# More Logging To Diagnose Silent Runs

## Background

A recent overnight-style run exited cleanly on its own after ~3m 29s but produced no log output past:

```
2026.04.23 21:20:45:318 [INF] Claude settings: Model="claude-opus-4-6", MaxTurns=100, ...
```

Claude clearly ran (new files appeared on disk), yet:
- No `[stdout]` / `[stderr]` lines reached the log.
- No `"Claude exited with code ..."` line.
- No outcome-handler activity.
- The task file remained in `tasks/pending/` instead of moving to `tasks/done/`, `tasks/blocked/`, or `tasks/failed/`.
- No top-level exception banner from the `catch (Exception ex)` in [CodeWorker/Program.cs](CodeWorker/Program.cs).

The only shapes consistent with a clean natural exit and no output past that line are:

1. Serilog events buffered and lost because `Log.CloseAndFlush()` is never called.
2. The child `claude` process emitted its output as a single blob at the very end (typical under `--output-format json`), and the `BeginOutputReadLine` handlers raced with process exit — the synthesized final line either fired on a thread-pool thread after `Run` returned or was dropped when the process exited.

This task adds logging and streaming changes to make the next run self-diagnose — without changing behavior beyond what logging requires.

---

## Objective

Instrument the runner so that any silent failure path produces enough log output on disk (and in a per-task live file) to identify which stage broke. Keep Serilog. Do not replace the logging framework.

---

## Scope

- [CodeWorker/Program.cs](CodeWorker/Program.cs) — guarantee flush on every exit path.
- [CodeWorker/Logging/SerilogConfiguration.cs](CodeWorker/Logging/SerilogConfiguration.cs) — shorten file-sink flush interval.
- [CodeWorker/Claude/ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs) — log full command + stdin size + output byte totals; provide `LiveLogPath`.
- [CodeWorker/Process/ProcessSettings.cs](CodeWorker/Process/ProcessSettings.cs) — add optional `LiveLogPath`.
- [CodeWorker/Process/RunProcess.cs](CodeWorker/Process/RunProcess.cs) — chunked async stream reads, lifecycle logs, heartbeat, live-log mirror.
- [CodeWorker/Commands/Run/ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) — trace each stage + catch/log/rethrow wrapper.
- [CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs](CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs), [HandleBlockedTaskOutcome.cs](CodeWorker/Commands/Run/Outcomes/HandleBlockedTaskOutcome.cs), [HandleFailedTaskOutcome.cs](CodeWorker/Commands/Run/Outcomes/HandleFailedTaskOutcome.cs) — entry + move logs (adds `ILogger` to each).
- [CodeWorker/Commands/Run/RunCommand.cs](CodeWorker/Commands/Run/RunCommand.cs) — extend the final summary log.

Tests:
- [CodeWorker.Tests/Commands/Run/ProcessTaskTests.cs](CodeWorker.Tests/Commands/Run/ProcessTaskTests.cs) — stage-trace and exception-rethrow tests.
- [CodeWorker.Tests/Claude/ClaudeRunnerTests.cs](CodeWorker.Tests/Claude/ClaudeRunnerTests.cs) — command + size logging tests.
- [CodeWorker.Tests/Commands/Run/Outcomes/HandleDoneTaskOutcomeTests.cs](CodeWorker.Tests/Commands/Run/Outcomes/HandleDoneTaskOutcomeTests.cs), `HandleBlockedTaskOutcomeTests.cs`, `HandleFailedTaskOutcomeTests.cs` — entry-log tests.

Untested (classes are `[ExcludeFromCodeCoverage]` per [errors-and-logging.md](.claude/rules/csharp/errors-and-logging.md) and [testing.md](.claude/rules/csharp/testing.md)):
- `RunProcess` (direct `System.Diagnostics.Process` wrapper).
- `Program` (entry point).
- `SerilogConfiguration` (config class).

---

## Design

### 1. Flush on every exit path — [Program.cs](CodeWorker/Program.cs)

Today `Main` has no `finally` and does not flush Serilog. Wrap the body so the flush runs whether `DoWork` succeeds, throws, or is cancelled.

```csharp
public static async Task Main(params string[] args)
{
    ConsoleLog.LogCallerInformation = true;

    AppDomain.CurrentDomain.ProcessExit += (_, _) => Log.CloseAndFlush();

    try
    {
        SystemScope.Initialize(
            new ContainerBuilder(),
            new List<Assembly> { typeof(Program).Assembly, typeof(ConsoleLog).Assembly },
            ScopeOptions.SetLifetimeScope
        );

        var application = SystemScope.Container.Resolve<CodeWorkerApplication>();

        await application.DoWork(args);
    }
    catch (Exception ex)
    {
        ConsoleLog.WriteException(ex);
    }
    finally
    {
        Log.CloseAndFlush();
        Console.Out.Flush();
    }
}
```

Rationale: Serilog's file sink runs its flush on a timer. Without an explicit close, events written in the last ~2 seconds of the process can be lost when the host tears down. `Console.Out.Flush()` defeats any block-buffering on redirected stdout (important when invoked by Task Scheduler, which does not attach to an interactive console).

### 2. Periodic file-sink flush — [SerilogConfiguration.cs:27-33](CodeWorker/Logging/SerilogConfiguration.cs#L27-L33)

Add a short flush interval so the on-disk log is current even if the process dies before `CloseAndFlush` runs:

```csharp
.WriteTo.File(
    logPath,
    fileSizeLimitBytes: 30000000,
    rollOnFileSizeLimit: true,
    retainedFileCountLimit: 100,
    formatProvider: new DateTimeLogFormatProvider(),
    flushToDiskInterval: TimeSpan.FromSeconds(1)
);
```

Rationale: 1-second flush bounds the worst-case lost window. It is the minimum-risk Serilog-native change that matches what a hand-rolled `StreamWriter` logger would guarantee.

### 3. Stream child-process output in real time — [RunProcess.cs](CodeWorker/Process/RunProcess.cs)

Replace `BeginOutputReadLine` / `BeginErrorReadLine` with two async reader tasks that loop on chunked `ReadAsync`. This does three things today's code cannot:

1. Fires even when the child never emits a newline (Claude's `--output-format json` produces one blob at exit).
2. Lets `Run` `await` the readers *after* `WaitForExitAsync` returns, guaranteeing the stdout/stderr pipes are drained before control leaves the method.
3. Writes every chunk to both Serilog and the per-task live log file (see item 5).

Shape (pseudocode — keep the existing `ProcessResult` shape):

```csharp
public async Task<ProcessResult> Run(ProcessSettings settings)
{
    var result = new ProcessResult();

    using var process = new System.Diagnostics.Process();
    process.StartInfo = CreateStartInfo(settings);

    await using var liveLogWriter = OpenLiveLogWriter(settings);

    logger.Information("Starting process {FileName} in {WorkingDirectory}", settings.FileName, settings.WorkingDirectory);

    if (!TryStart(process, result, settings))
    {
        return result;
    }

    logger.Information("Process started, PID={ProcessId}", process.Id);

    await WriteStandardInput(process, settings);

    var stdoutReader = StreamOutput(process.StandardOutput, result.OutputLines, "stdout", liveLogWriter);
    var stderrReader = StreamOutput(process.StandardError, result.ErrorLines, "stderr", liveLogWriter);
    var heartbeat = StartHeartbeat(process.Id, settings.FileName, result);

    await WaitForExit(process, result, settings);

    heartbeat.Stop();

    await Task.WhenAll(stdoutReader, stderrReader);

    if (!result.TimedOut)
    {
        result.ExitCode = process.ExitCode;
    }

    logger.Information(
        "Process exited. ExitCode={ExitCode}, TimedOut={TimedOut}, stdoutBytes={StdoutBytes}, stderrBytes={StderrBytes}",
        result.ExitCode,
        result.TimedOut,
        result.OutputLines.Sum(line => line.Length),
        result.ErrorLines.Sum(line => line.Length)
    );

    return result;
}
```

`StreamOutput` details:
- Loops on `reader.ReadAsync(buffer, cancellationToken)` with a 4 KB `char[]`.
- On each chunk: appends the chunk's string to a per-stream `StringBuilder`, splits on newlines to populate `result.OutputLines` / `result.ErrorLines` (preserving the existing shape used by `ClassifyTaskResult`), writes the chunk through `logger.Information("[{Stream}] {Chunk}", streamName, chunk)`, and writes through `liveLogWriter`.
- On `-1` return or end-of-stream: flushes any remaining partial line and exits.

`ProcessResult.OutputLines` / `ErrorLines` remain `List<string>` — `ClassifyTaskResult.HasBlockedMarker` continues to work unchanged.

### 4. Heartbeat while waiting — [RunProcess.cs](CodeWorker/Process/RunProcess.cs)

Tiny local helper started before `WaitForExit`, stopped after. Logs every 30 seconds:

```
[INF] Still waiting on claude (PID=1234) — elapsed 00:01:30, stdoutBytes=0, stderrBytes=0, lastReadAgo=00:01:30
```

`lastReadAgo` is the wall-clock gap since the last non-empty chunk. If bytes climb, Claude is producing. If bytes are zero and `lastReadAgo` grows, Claude is hung. The heartbeat exists entirely to make a silent 3-minute wait impossible again.

Heartbeat is cancelled deterministically after `WaitForExitAsync` returns (either `CancellationTokenSource.Cancel()` + await the timer task, or `PeriodicTimer`).

### 5. Per-task live log file — [ProcessSettings.cs](CodeWorker/Process/ProcessSettings.cs), [ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs), [RunProcess.cs](CodeWorker/Process/RunProcess.cs)

Add one optional field to `ProcessSettings`:

```csharp
public string LiveLogPath { get; set; }
```

When set, `RunProcess` opens a `FileStream` in append mode with `FileOptions.WriteThrough`, wraps it in a `StreamWriter`, and writes every chunk with a `[stdout]` / `[stderr]` prefix and a timestamp. Closes on return.

`ClaudeRunner.Run` computes the live log path from `markdownFilePath`:

```
tasks/pending/01-MoreLogOnDone.md  →  tasks/pending/01-MoreLogOnDone.live.log
```

The live file exists next to the task while it is running, enabling `tail -f`. After the run, the file stays put — it is a running-state artifact. The current `LogTaskResult` aggregate log and any future per-task `.log` are unaffected.

Gating: the live log is always written when `LogResults` is enabled on the repo settings. No additional toggle.

### 6. Richer Claude-runner logging — [ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs)

Before `runProcess.Run`, add:

```csharp
logger.Information("Claude command: {FileName} {Arguments}", settings.FileName, settings.Arguments);
logger.Information("Claude stdin length: {StdinLength} chars", settings.StandardInput?.Length ?? 0);
logger.Information(
    "Claude reference files: {Count}, total reference prompt length: {Length}",
    referenceFiles?.Count ?? 0,
    referenceContent?.Length ?? 0
);
```

(The reference content string has to be captured once — today it is built inside `AppendReferenceFiles` and discarded. Extract a local in `BuildArguments` so the length is available to log. No behavior change.)

After `runProcess.Run` returns, the existing `"Claude exited with code ..."` line stays; the new summary added in `RunProcess` (item 3) provides the byte totals.

### 7. Trace `ProcessTask.Run` stages — [ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs)

Wrap the body in `try/catch` that logs and rethrows, and bracket each stage:

```csharp
public async Task<TaskProcessingDecision> Run(TaskExecutionContext executionContext, string taskFile)
{
    // ...existing setup...

    try
    {
        logger.Information("Starting task {TaskName}", task.TaskName);

        logger.Information("Moving task {TaskName} to pending", task.TaskName);
        moveTask.Move(task.TaskFile, context.Folders.Pending);
        logger.Information("Task {TaskName} moved to pending", task.TaskName);

        logger.Information("Invoking Claude for {TaskName}", task.TaskName);
        task.Result = await runClaude.Run(task.PendingFilePath, context.ClaudeSettings, context.ReferenceFiles);
        logger.Information(
            "Claude run returned for {TaskName}: ExitCode={ExitCode}, TimedOut={TimedOut}, FailedToStart={FailedToStart}, OutputLines={OutputLineCount}, ErrorLines={ErrorLineCount}",
            task.TaskName,
            task.Result.ExitCode,
            task.Result.TimedOut,
            task.Result.FailedToStart,
            task.Result.OutputLines.Count,
            task.Result.ErrorLines.Count
        );

        await LogResultIfEnabled();

        var outcome = classifyTaskResult.Classify(task.Result);
        logger.Information("Classified task {TaskName} as {Outcome}", task.TaskName, outcome);

        await RecordHistory(outcome);
        logger.Information("Recorded run history for {TaskName}", task.TaskName);

        logger.Information("Invoking outcome handler for {Outcome} on {TaskName}", outcome, task.TaskName);
        var decision = await outcomeHandlerFactory.For(outcome).Handle(context, task);
        logger.Information("Outcome handler complete for {TaskName}", task.TaskName);

        return decision;
    }
    catch (Exception exception)
    {
        logger.Error(exception, "Unhandled exception processing task {TaskName}", task.TaskName);
        throw;
    }
}
```

Rationale: today the top-level `catch` in `Program.Main` uses `ConsoleLog.WriteException`, which does not go through Serilog. If an exception is thrown deep inside `ProcessTask.Run`, it will not appear in the Serilog file sink. The inner `logger.Error(exception, ...)` guarantees it does.

### 8. Outcome-handler entry logs

Each of the three handlers gains `ILogger` via its primary constructor and logs entry + move:

```csharp
public class HandleDoneTaskOutcome(IMoveTask moveTask, IRunGitWorkflow runGitWorkflow, ILogger logger) : ITaskOutcomeHandler
{
    public async Task<TaskProcessingDecision> Handle(TaskExecutionContext context, TaskExecution task)
    {
        logger.Information("Handling Done outcome for {TaskName}: moving to {Destination}", task.TaskName, context.Folders.Done);

        moveTask.Move(task.PendingFilePath, context.Folders.Done);

        logger.Information("Moved {TaskName} to {Destination}", task.TaskName, context.Folders.Done);

        return await runGitWorkflow.Run(context, task);
    }
}
```

Same pattern for `HandleBlockedTaskOutcome` and `HandleFailedTaskOutcome`. With this in place, a task stuck in `pending/` can be pinned to either "handler never ran" or "move threw" just from the log.

### 9. Runner completion summary — [RunCommand.cs:32](CodeWorker/Commands/Run/RunCommand.cs#L32)

Extend:

```csharp
logger.Information(
    "Task runner complete — processed {RepositoryCount} repositories in {DurationSeconds}s",
    settings.Repositories.Count,
    stopwatch.Elapsed.TotalSeconds
);
```

Wrap the foreach in a `Stopwatch`. This line is the canary — absence means the outer loop never reached the end.

---

## TDD Plan

All tests live in their mirrored test namespace and use `BddBase`, `A.Fake<T>()`, `FluentAssertions`, and `Faker.Create<T>()`. Logging is exempt from strict TDD per [errors-and-logging.md](.claude/rules/csharp/errors-and-logging.md), but the structural changes below are testable and should be written test-first.

### `ProcessTaskTests` — [CodeWorker.Tests/Commands/Run/ProcessTaskTests.cs](CodeWorker.Tests/Commands/Run/ProcessTaskTests.cs)

1. `RethrowExceptionsFromMoveTask` — `moveTask.Move` throws, assert the same exception bubbles out of `Run`.
2. `LogErrorWhenExceptionIsThrown` — same setup, assert `logger.Error(exception, ...)` was called with the task name. Use `A.CallTo(() => logger.Error(A<Exception>._, A<string>._, A<object[]>._))`.
3. `RethrowExceptionsFromRunClaude` — same shape for `runClaude.Run`.
4. `RethrowExceptionsFromClassifyTaskResult`.
5. `RethrowExceptionsFromOutcomeHandler`.
6. `InvokeOutcomeHandlerOnlyAfterHistoryIsRecorded` — ordering assertion via `A.CallTo(...).MustHaveHappenedOnceExactly().Then(...)`. Catches regressions where the handler runs before history.

Log-content tests are not required for the per-stage `logger.Information` lines (logging exemption), but the structural ordering above is load-bearing.

### `ClaudeRunnerTests` — [CodeWorker.Tests/Claude/ClaudeRunnerTests.cs](CodeWorker.Tests/Claude/ClaudeRunnerTests.cs)

7. `PassLiveLogPathDerivedFromMarkdownFile` — assert `runProcess.Run` is called with a `ProcessSettings` whose `LiveLogPath` equals `<markdownFilePath without extension>.live.log`.
8. `PassTheReferenceFilesToBuildReferenceSystemPrompt` — existing coverage may already exist; confirm it survives the extracted local.

Command-string and size logs are logging-only and skipped for TDD.

### Outcome-handler tests

9. `HandleDoneTaskOutcomeTests.LogBeforeMove` — assert `logger.Information("Handling Done outcome ...", ...)` was called.
10. `HandleDoneTaskOutcomeTests.LogAfterMove`.
11. Same two tests for `HandleBlockedTaskOutcomeTests` and `HandleFailedTaskOutcomeTests`.

These are worth writing because they guarantee the move-failure diagnostic path cannot regress silently.

### No tests for

- `RunProcess` — `[ExcludeFromCodeCoverage]` per HEAD justification.
- `Program` / `SerilogConfiguration` — infrastructure.
- `RunCommand` completion log — logging-only change.

---

## Implementation Order

**Phase 1 — Flush hygiene (low risk, high payoff)**

1. Add `Log.CloseAndFlush()` + `ProcessExit` handler + `Console.Out.Flush()` in [Program.cs](CodeWorker/Program.cs).
2. Add `flushToDiskInterval: TimeSpan.FromSeconds(1)` to [SerilogConfiguration.cs](CodeWorker/Logging/SerilogConfiguration.cs).
3. `dotnet build` — verify nothing else broke.

**Phase 2 — Process streaming**

4. Add `LiveLogPath` to [ProcessSettings.cs](CodeWorker/Process/ProcessSettings.cs).
5. Rewrite [RunProcess.cs](CodeWorker/Process/RunProcess.cs): chunked async reads, heartbeat, live-log mirror, drained-before-return, lifecycle logs. `ProcessResult` shape unchanged.
6. Wire `LiveLogPath` from [ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs). Write `ClaudeRunnerTests` test 7 first.

**Phase 3 — Stage tracing and handlers**

7. Write `ProcessTaskTests` 1-6 first. They fail.
8. Add try/catch + `logger.Error` rethrow + per-stage logs to [ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs). Tests go green.
9. Write outcome-handler tests 9-11 first. They fail.
10. Add `ILogger` to each handler's primary constructor and emit entry/exit logs. Tests go green. Confirm Autofac still resolves one implementation per interface — no module change needed per [types-and-di.md](.claude/rules/csharp/types-and-di.md).

**Phase 4 — Finish**

11. Extend `RunCommand` completion log with duration + count.
12. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`.

---

## Constraints

- Keep Serilog. Do not replace the logging framework — contradicts [.claude/rules/csharp/errors-and-logging.md](.claude/rules/csharp/errors-and-logging.md).
- Do not change the `ProcessResult` shape — `ClassifyTaskResult` and `LogTaskResult` depend on it.
- Do not change the public signatures of `IRunClaude`, `IRunProcess`, `IProcessTask`, or `ITaskOutcomeHandler`. Constructor additions are allowed because primary constructors + Autofac handle the wiring automatically.
- No `DevLog` in permanent code per [errors-and-logging.md](.claude/rules/csharp/errors-and-logging.md).
- No `ConfigureAwait(false)` per [async.md](.claude/rules/csharp/async.md).
- No `async void`; readers and heartbeat return `Task`.
- No expression-bodied members per [toolchain.md](.claude/rules/csharp/toolchain.md).
- Follow every rule in `.claude/rules/csharp/` — no exceptions.

---

## Acceptance Criteria

Log-flush:
- After a task completes, the on-disk `Logs/CodeWorker.log` contains at least the `"Task runner complete — processed N repositories ..."` line. The absence of that line on a run that reached the end is a regression.

Process streaming:
- Every stdout chunk emitted by the child process appears in `Logs/CodeWorker.log` as `[stdout] ...` within one second of being written by the child.
- A run that ends normally has a `"Process exited. ExitCode=..., stdoutBytes=N, stderrBytes=M"` line.
- The per-task live log file `tasks/pending/<task-name>.live.log` exists during the run and contains every chunk from both streams.

Heartbeat:
- Any run where the child takes longer than 30 seconds produces at least one `"Still waiting on claude ..."` line per 30-second window.

Stage tracing:
- A run where `ProcessTask.Run` reaches each stage produces all of: `"Moving task ... to pending"`, `"Invoking Claude ..."`, `"Claude run returned ..."`, `"Classified task ... as ..."`, `"Recorded run history ..."`, `"Invoking outcome handler ..."`, `"Outcome handler complete ..."`.
- Any exception thrown inside `ProcessTask.Run` produces a `logger.Error(exception, "Unhandled exception processing task {TaskName}", ...)` line and the exception still propagates to `Program.Main`.

Outcome handlers:
- Each handler logs both a `"Handling <Outcome> outcome for ..."` line on entry and a `"Moved ... to ..."` line after the move.

Build:
- `dotnet build`, `dotnet test`, `dotnet format` all clean.
- No new compiler warnings.

---

## Verification

- [ ] Tests written before implementation (TDD) for items 1-6, 7, 9-11.
- [ ] Logging-only changes follow the documented TDD exemption; no tests added for log-string content.
- [ ] No compiler warnings introduced.
- [ ] Namespaces match folder paths exactly.
- [ ] Must follow all rules `.claude\rules\csharp` no exceptions.
- [ ] No banned patterns used (see `.claude/rules/csharp/not-allowed.md`).
- [ ] All tests pass (`dotnet test`).
- [ ] `dotnet format` run on all modified files.
- [ ] `dotnet build` to apply CSharpier changes.
- [ ] Report results before finishing.
