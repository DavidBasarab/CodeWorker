# Stall Kill-Switch For Silent Claude Runs

## Background

A recent run showed the Claude child process producing **zero bytes** on stdout and stderr for 4+ minutes while the heartbeat kept firing:

```
Still waiting on "claude" (PID=16540) — elapsed 00:04:00.0971582, stdoutBytes=0, stderrBytes=0, lastReadAgo=00:04:00.0984907
```

The heartbeat already measures exactly the signal we need (`TimeSinceLastChunk`, `StdoutBytes`, `StderrBytes`) — it just does not act on it. Today the run can only be torn down by the 90-minute timeout, which chews hours of wall clock time on a stuck child.

This plan adds an explicit "no output for N minutes" kill-switch inside [CodeWorker/Process/RunProcess.cs](CodeWorker/Process/RunProcess.cs) so stuck runs die fast and the outer process moves on.

---

## Objective

When a child process has produced no stdout and no stderr for longer than a configured threshold, kill the process tree, flag the run as stalled, and return a `ProcessResult` distinct from a timeout so callers can classify it correctly.

---

## Scope

- [CodeWorker/Process/ProcessSettings.cs](CodeWorker/Process/ProcessSettings.cs) — add `NoOutputKillMilliseconds`.
- [CodeWorker/Process/ProcessResult.cs](CodeWorker/Process/ProcessResult.cs) — add `Stalled` flag. Do **not** reuse `TimedOut` — classification wants to tell the two apart.
- [CodeWorker/Process/RunProcess.cs](CodeWorker/Process/RunProcess.cs) — detect stall in the existing heartbeat loop, kill tree, set `Stalled`, unblock `WaitForExit`.
- [CodeWorker/Claude/ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs) — populate `NoOutputKillMilliseconds` from a new `ClaudeSettings.NoOutputKillMinutes`.
- [CodeWorker/Settings/ClaudeSettings.cs](CodeWorker/Settings/ClaudeSettings.cs) — add `NoOutputKillMinutes` + merge support.
- [CodeWorker/appsettings.json](CodeWorker/appsettings.json) — default value (suggested: 10 minutes).
- [CodeWorker/Commands/Run/ClassifyTaskResult.cs](CodeWorker/Commands/Run/ClassifyTaskResult.cs) — classify `Stalled` the same as `Failed` (it is a failure state — the child never produced work we can verify).
- [CodeWorker/Commands/Run/WriteTaskLog.cs](CodeWorker/Commands/Run/WriteTaskLog.cs) / [LogTaskResult.cs](CodeWorker/Commands/Run/LogTaskResult.cs) — include `Stalled` in the per-task log body.

Tests:
- [CodeWorker.Tests/Claude/ClaudeRunnerTests.cs](CodeWorker.Tests/Claude/ClaudeRunnerTests.cs) — `PassNoOutputKillFromClaudeSettings`, `NoOutputKillFlowsThroughProcessSettings`.
- [CodeWorker.Tests/Commands/Run/ClassifyTaskResultTests.cs](CodeWorker.Tests/Commands/Run/ClassifyTaskResultTests.cs) — `ClassifyStalledAsFailed`.
- Settings merge tests under `CodeWorker.Tests/Settings/` — `MergeRespectsNoOutputKillOverride`, `MergeFallsBackToBaseNoOutputKillWhenOverrideIsZero`.

Untested (`[ExcludeFromCodeCoverage]` justified):
- `RunProcess` — direct `System.Diagnostics.Process` wrapper.

---

## Design

### 1. New settings field — `ClaudeSettings.NoOutputKillMinutes`

```csharp
public int NoOutputKillMinutes { get; set; }
```

Merge rule: `overrides.NoOutputKillMinutes > 0 ? overrides.NoOutputKillMinutes : NoOutputKillMinutes`, matching the pattern already used by `TimeoutMinutes` in [ClaudeSettings.cs:31](CodeWorker/Settings/ClaudeSettings.cs#L31).

Default in [appsettings.json](CodeWorker/appsettings.json): `10`. A ten-minute window is long enough that a slow Claude run (large tool-call batches, big file diffs) will not be killed by accident, and short enough that a stuck child does not burn the full 90-minute `TimeoutMinutes`. `0` disables the kill-switch.

### 2. New `ProcessSettings` field — `NoOutputKillMilliseconds`

```csharp
public int NoOutputKillMilliseconds { get; set; }
```

Wired in [ClaudeRunner.cs:41](CodeWorker/Claude/ClaudeRunner.cs#L41) parallel to `TimeoutMilliseconds`:

```csharp
NoOutputKillMilliseconds = claudeSettings.NoOutputKillMinutes > 0
    ? claudeSettings.NoOutputKillMinutes * 60 * 1000
    : 0,
```

`0` means "no stall detection" so other callers of `IRunProcess` are unaffected.

### 3. New `ProcessResult` field — `Stalled`

```csharp
public bool Stalled { get; set; }
```

Separate from `TimedOut` because:
- `TimedOut` means "overall budget exceeded — child may have been productive the whole time".
- `Stalled` means "child went quiet — almost certainly waiting on a prompt or deadlocked".

The two have different remediation paths (raise the timeout vs. fix auth / wrap the input differently), so merging them loses information.

### 4. Detect the stall — [RunProcess.cs](CodeWorker/Process/RunProcess.cs)

The existing `RunHeartbeat` loop already reads `streamState.StdoutBytes`, `streamState.StderrBytes`, and `streamState.TimeSinceLastChunk()`. Extend it with a kill branch:

```csharp
private async Task RunHeartbeat(
    int processId,
    string fileName,
    StreamReadState streamState,
    System.Diagnostics.Process process,
    ProcessResult result,
    int noOutputKillMilliseconds,
    CancellationToken cancellationToken
)
{
    var elapsed = Stopwatch.StartNew();

    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(HeartbeatInterval, cancellationToken);

            var sinceLastChunk = streamState.TimeSinceLastChunk();
            var totalBytes = streamState.StdoutBytes + streamState.StderrBytes;

            logger.Information(
                "Still waiting on {FileName} (PID={ProcessId}) — elapsed {Elapsed}, stdoutBytes={StdoutBytes}, stderrBytes={StderrBytes}, lastReadAgo={LastReadAgo}",
                fileName,
                processId,
                elapsed.Elapsed,
                streamState.StdoutBytes,
                streamState.StderrBytes,
                sinceLastChunk
            );

            if (ShouldKillForStall(noOutputKillMilliseconds, totalBytes, sinceLastChunk))
            {
                logger.Error(
                    "Process {FileName} (PID={ProcessId}) produced no output for {SinceLastChunk} — exceeded stall threshold {Threshold}, killing process tree",
                    fileName,
                    processId,
                    sinceLastChunk,
                    TimeSpan.FromMilliseconds(noOutputKillMilliseconds)
                );

                result.Stalled = true;
                result.ExitCode = -1;
                result.ErrorLines.Add(
                    $"Process stalled — no output for {sinceLastChunk}. Killed after {noOutputKillMilliseconds / 60000} minute(s)."
                );

                process.Kill(entireProcessTree: true);

                return;
            }
        }
    }
    catch (OperationCanceledException)
    {
        // ignored — heartbeat cancellation is expected when the process exits
    }
}

private bool ShouldKillForStall(int noOutputKillMilliseconds, long totalBytes, TimeSpan sinceLastChunk)
{
    if (noOutputKillMilliseconds <= 0)
    {
        return false;
    }

    if (totalBytes > 0)
    {
        return false;
    }

    return sinceLastChunk.TotalMilliseconds >= noOutputKillMilliseconds;
}
```

Key rules:
- Only triggers when `totalBytes == 0`. A process that emitted something and then went quiet is a different failure mode (hung mid-run) and should be handled by `TimeoutMilliseconds`, not by the stall kill-switch. We can extend later if needed, but a conservative first cut prevents false kills.
- Fires `process.Kill(entireProcessTree: true)`, which unblocks the awaiting `WaitForExitAsync`.
- Sets `result.Stalled` **before** killing so there is no race with the main thread reading `ExitCode`.
- `ExitCode = -1` matches the `TimedOut` convention already in the file.

After the kill, `Run` naturally falls through: `WaitForExit` returns, `stdoutReader` / `stderrReader` drain, the final `"Process exited."` line logs. The only adjustment in `Run` itself is to leave `result.ExitCode` alone when `result.Stalled` is set, paralleling the existing `if (!result.TimedOut)` guard:

```csharp
if (!result.TimedOut && !result.Stalled)
{
    result.ExitCode = process.ExitCode;
}
```

### 5. Classify `Stalled` as `Failed` — [ClassifyTaskResult.cs](CodeWorker/Commands/Run/ClassifyTaskResult.cs)

A stalled run did not produce verifiable work. Route it to `tasks/failed/` via the existing `Failed` outcome rather than inventing a new `TaskOutcome`:

```csharp
if (result.Stalled)
{
    return TaskOutcome.Failed;
}
```

Place above the existing `TimedOut` branch so both are handled before the exit-code check. Rationale: reusing `Failed` avoids touching `TaskFolders`, `ITaskOutcomeHandlerFactory`, and the folder layout.

### 6. Surface `Stalled` in the per-task log

Add the flag to the log body in [WriteTaskLog.cs](CodeWorker/Commands/Run/WriteTaskLog.cs) alongside `TimedOut`:

```
Stalled:        true
```

The per-task log is the first place an operator looks, so the flag needs to be visible without running the binary.

---

## TDD Plan

### `ClaudeRunnerTests`

1. `PassNoOutputKillMillisecondsFromClaudeSettings` — `ClaudeSettings.NoOutputKillMinutes = 5` → `ProcessSettings.NoOutputKillMilliseconds = 300_000`.
2. `SetNoOutputKillMillisecondsToZeroWhenNoOutputKillMinutesIsZero` — disables stall detection.
3. `SetNoOutputKillMillisecondsToZeroWhenNoOutputKillMinutesIsNegative` — defensive; treat negative as disabled.

### `ClaudeSettingsTests`

4. `MergeRespectsNoOutputKillOverride`.
5. `MergeFallsBackToBaseNoOutputKillWhenOverrideIsZero`.

### `ClassifyTaskResultTests`

6. `ClassifyStalledResultAsFailed` — `new ProcessResult { Stalled = true }` → `TaskOutcome.Failed`.
7. `StalledBeatsExitCodeZero` — `Stalled = true, ExitCode = 0` still maps to `Failed`. Guards against the ordering bug where a stall sneaks past because the exit code was never updated.

### `WriteTaskLogTests`

8. `IncludeStalledFlagInTheLogBody` — body contains `Stalled:` with the bool.

### Untested

- `RunProcess` stall detection — `[ExcludeFromCodeCoverage]`. The logic in `ShouldKillForStall` is deliberately extracted as a pure private method so future us can lift it into its own `[ExcludeFromCodeCoverage]`-free class if we ever want direct unit tests. Not in this phase.

---

## Implementation Order

**Phase 1 — Settings plumbing**

1. Write `ClaudeSettingsTests` 4-5. Fail.
2. Add `NoOutputKillMinutes` to `ClaudeSettings` + `MergeWith`. Green.
3. Write `ClaudeRunnerTests` 1-3. Fail.
4. Add `NoOutputKillMilliseconds` to `ProcessSettings`. Wire it in `ClaudeRunner`. Green.

**Phase 2 — Result shape**

5. Add `Stalled` to `ProcessResult` (no tests — POCO).
6. Write `ClassifyTaskResultTests` 6-7. Fail.
7. Add the `Stalled` branch to `ClassifyTaskResult`. Green.

**Phase 3 — Log surfacing**

8. Write `WriteTaskLogTests` 8. Fail.
9. Add the `Stalled:` line to the body composer. Green.

**Phase 4 — Kill path**

10. Extend `RunProcess.RunHeartbeat` to detect the stall and call `Kill(entireProcessTree: true)` per the design above.
11. Guard `ExitCode` assignment in `Run` with `!result.Stalled`.
12. Manual smoke test: run against a task that reliably hangs (or a fake child that reads stdin and sleeps forever), confirm the process is killed at the configured threshold, the live log captures the `Process stalled — no output for ...` error line, and the task file moves to `tasks/failed/`.

**Phase 5 — Finish**

13. Set `NoOutputKillMinutes: 10` in [appsettings.json](CodeWorker/appsettings.json).
14. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`.

---

## Constraints

- Do not reuse `TimedOut` for stall — the two failure modes need to be distinguishable in logs and history.
- Do not change the `ITaskOutcomeHandlerFactory` surface — `Stalled` classifies to the existing `Failed` handler.
- Do not change `IRunProcess`. The new field lives on `ProcessSettings`, so existing callers that omit it keep the previous behavior.
- Follow every rule in `.claude/rules/csharp/` — primary constructors, block-body methods, `switch` expressions with discard arms, interfaces describe capabilities.
- No `async void`. Heartbeat and readers continue to return `Task`.
- No `ConfigureAwait(false)`.
- No `DevLog` in permanent code.

---

## Acceptance Criteria

- A child process that writes no bytes to stdout or stderr for `NoOutputKillMinutes` minutes is killed (entire process tree) before `TimeoutMinutes` elapses.
- After the kill, `ProcessResult.Stalled == true`, `ProcessResult.ExitCode == -1`, and `ErrorLines` contains the stall-kill message.
- `ClassifyTaskResult` maps a `Stalled` result to `TaskOutcome.Failed`.
- The task file moves to `tasks/failed/` and a per-task log appears in the same folder.
- The per-task log body contains a `Stalled: true` line for a stalled run and `Stalled: false` for every other outcome.
- `NoOutputKillMinutes = 0` disables the kill-switch — no false positives on slow-but-progressing runs.
- A run that emits even a single byte and then goes quiet is **not** killed by the stall switch; it remains governed by `TimeoutMinutes`.
- A run that completes normally produces no "stalled" log line and `Stalled == false`.
- `dotnet build`, `dotnet test`, `dotnet format` all clean.

---

## Verification

- [ ] Tests written before implementation (TDD) for items 1-8.
- [ ] `RunProcess` change exercised manually against a deliberately hung child; log output captured and attached to the task log.
- [ ] No compiler warnings introduced.
- [ ] Namespaces match folder paths exactly.
- [ ] Must follow all rules `.claude\rules\csharp` — no exceptions.
- [ ] No banned patterns used (see `.claude/rules/csharp/not-allowed.md`).
- [ ] All tests pass (`dotnet test`).
- [ ] `dotnet format` run on all modified files.
- [ ] `dotnet build` to apply CSharpier changes.
- [ ] Report results before finishing.
