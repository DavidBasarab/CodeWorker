# Relocate Task Logs to a Configurable Logs Folder

## Objective
Two related changes that together clean up where task-related log files live after a run:

1. **Stop leaving `.live.log` files behind in `tasks/pending/`.** When a task finishes and its `.md` file moves out of pending, the matching `.live.log` (written by [CodeWorker/Process/RunProcess.cs](CodeWorker/Process/RunProcess.cs) via the `LiveLogPath` derived in [CodeWorker/Claude/ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs)) is currently orphaned in `tasks/pending/`. Move it so it follows the task.
2. **Make the destination of per-task log files configurable.** The post-run `.log` file produced by [CodeWorker/Commands/Run/WriteTaskLog.cs](CodeWorker/Commands/Run/WriteTaskLog.cs) is currently written next to the moved task file (e.g. `tasks/done/<task>.log`). Add a setting that controls where these go. The default should be a dedicated `tasks/logs/` folder. The new home applies to **both** the post-run `.log` file and the moved-from-pending `.live.log`.

The result: after a run, `tasks/pending/` is empty, `tasks/done/` (or `blocked`/`failed`) holds only the `.md` file, and `tasks/logs/` holds the matching `<task>.log` and `<task>.live.log` pair. Operators who want to keep logs adjacent to their outcome folder can override `LogsFolder` in `tasks/settings.json` to e.g. `tasks/done/logs`.

## Scope
- [CodeWorker/Settings/TaskSettings.cs](CodeWorker/Settings/TaskSettings.cs) — add `LogsFolder` property
- [CodeWorker/Commands/Setup/defaultSettings.json](CodeWorker/Commands/Setup/defaultSettings.json) — add `LogsFolder` default
- [CodeWorker/Commands/Run/TaskFolders.cs](CodeWorker/Commands/Run/TaskFolders.cs) — add `Logs` property
- [CodeWorker/Commands/Run/BuildTaskFolders.cs](CodeWorker/Commands/Run/BuildTaskFolders.cs) — populate `Logs`
- [CodeWorker/Commands/Run/WriteTaskLog.cs](CodeWorker/Commands/Run/WriteTaskLog.cs) — write to `Folders.Logs` instead of the outcome folder
- [CodeWorker/Commands/Run/ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) — call site that moves the `.live.log` after the outcome handler runs
- New classes + tests:
  - `MoveLiveLog` / `MoveLiveLogTests` — single-purpose class that moves `<pending>/<task>.live.log` to `<logsFolder>/<task>.live.log`
- Updated tests:
  - `WriteTaskLogTests` — assert path now uses `Folders.Logs`
  - `BuildTaskFoldersTests` — assert `Logs` is populated from `LogsFolder`
  - `ProcessTaskTests` — assert `MoveLiveLog.Move(...)` runs after the outcome handler
  - `LoadRepoSettingsTests` (or whichever exercises JSON deserialization) — confirm `LogsFolder` round-trips
- Existing integration / setup-validation tests that read `defaultSettings.json` must still pass

Out of scope:
- Existing aggregate `CodeWorker.log` (still written via [LogTaskResult.cs](CodeWorker/Commands/Run/LogTaskResult.cs)) — unchanged.
- Existing `tasks/run-history.jsonl` — unchanged.
- The global `runs.jsonl` — unchanged.
- The `LiveLogPath` derivation in [ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs) — unchanged. The `.live.log` is still produced next to the task file in `tasks/pending/` while Claude runs. We **move** it after, not redirect it during execution. Rationale: redirecting at write-time would require plumbing the logs folder into `ClaudeRunner`/`RunProcess`, change a stable hot path, and complicate any future "tail the live log" tooling that watches `pending/`.

## Design

### Setting
Add `LogsFolder` to `TaskSettings`:

```csharp
public class TaskSettings
{
    public string TodoFolder { get; set; }
    public string DoneFolder { get; set; }
    public string ReferenceFolder { get; set; }
    public string PendingFolder { get; set; }
    public string BlockedFolder { get; set; }
    public string FailedFolder { get; set; }
    public string LogsFolder { get; set; }   // new
    public bool StopOnBlocked { get; set; }
    public bool StopOnFailed { get; set; }
    public bool RunPlanningPhase { get; set; }
}
```

`defaultSettings.json` gets the matching key:

```json
"Tasks": {
    "TodoFolder": "tasks/todo",
    "DoneFolder": "tasks/done",
    "ReferenceFolder": "tasks/reference",
    "PendingFolder": "tasks/pending",
    "BlockedFolder": "tasks/blocked",
    "FailedFolder": "tasks/failed",
    "LogsFolder": "tasks/logs",
    ...
}
```

### Resolved paths
`TaskFolders` gets a matching `Logs` property. `BuildTaskFolders.Build` joins it with `repositoryPath` exactly like every other folder:

```csharp
return new TaskFolders
{
    Todo      = Path.Combine(repositoryPath, tasks.TodoFolder),
    Pending   = Path.Combine(repositoryPath, tasks.PendingFolder),
    Done      = Path.Combine(repositoryPath, tasks.DoneFolder),
    Blocked   = Path.Combine(repositoryPath, tasks.BlockedFolder),
    Failed    = Path.Combine(repositoryPath, tasks.FailedFolder),
    Reference = Path.Combine(repositoryPath, tasks.ReferenceFolder),
    Logs      = Path.Combine(repositoryPath, tasks.LogsFolder),
};
```

### Where the post-run `.log` goes
`WriteTaskLog.Write` currently picks the destination off the outcome:

```csharp
var destinationFolder = outcome switch
{
    TaskOutcome.Done    => context.Folders.Done,
    TaskOutcome.Blocked => context.Folders.Blocked,
    TaskOutcome.Failed  => context.Folders.Failed,
    _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
};
```

Replace the switch with a single line:

```csharp
var destinationFolder = context.Folders.Logs;
```

The outcome value still appears in the log body — the switch is only used for path selection, and the log body composition is unchanged. The discard-arm test for unhandled outcomes goes away with the switch (see Test plan changes below).

### Moving the `.live.log`
New class `CodeWorker/Commands/Run/MoveLiveLog.cs` with an inline interface (single consumer — `ProcessTask` — so the inline pattern from [naming-and-structure.md](.claude/rules/csharp/naming-and-structure.md) applies):

```csharp
public interface IMoveLiveLog
{
    void Move(TaskExecutionContext context, TaskExecution task);
}

public class MoveLiveLog(IMoveFile moveFile, IFileSystemTools fileSystemTools, ILogger logger) : IMoveLiveLog
{
    public void Move(TaskExecutionContext context, TaskExecution task)
    {
        var sourcePath      = Path.Combine(context.Folders.Pending, $"{Path.GetFileNameWithoutExtension(task.TaskName)}.live.log");
        var destinationPath = Path.Combine(context.Folders.Logs,    $"{Path.GetFileNameWithoutExtension(task.TaskName)}.live.log");

        if (!fileSystemTools.FileExists(sourcePath))
        {
            logger.Information("No live log to move for {TaskName}", task.TaskName);
            return;
        }

        logger.Information("Moving live log for {TaskName} to {DestinationPath}", task.TaskName, destinationPath);

        moveFile.Move(sourcePath, destinationPath);
    }
}
```

Notes:
- The class checks for the source's existence because a task whose live log failed to open (see the warning at [RunProcess.cs:119](CodeWorker/Process/RunProcess.cs)) will not have a `.live.log` file. The move must not throw in that case. `IFileSystemTools.FileExists` is the canonical existence check used elsewhere in this project — confirm this name when implementing; if the abstraction does not yet expose it, add the method to `IFileSystemTools` and its concrete implementation alongside this work.
- `IMoveFile` already exists ([CodeWorker/FileSystem/MoveFile.cs](CodeWorker/FileSystem/MoveFile.cs)) — reuse it. Do not introduce a parallel filesystem move.
- The method is synchronous because `IMoveFile.Move` is synchronous. Do not invent an async wrapper.

### Where it gets called
[ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) currently does:

```text
moveTask.Move(taskFile, Folders.Pending)
runClaude.Run(...)                        // writes pending/<task>.live.log
LogResultIfEnabled
classify outcome
WriteTaskLogIfEnabled                     // writes <Folders.Logs>/<task>.log AFTER this PR
RecordHistory
RecordRepositoryHistory
outcomeHandlerFactory.For(outcome).Handle // moves pending/<task>.md → outcome folder
```

Add the live-log move **after** the outcome handler returns:

```csharp
var decision = await outcomeHandlerFactory.For(outcome).Handle(context, task);
moveLiveLog.Move(context, task);
return decision;
```

It runs after — not before — the outcome handler so the order is: task `.md` moves out of pending first, then its sibling `.live.log` is relocated. That keeps `tasks/pending/` empty for **all three** outcomes (Done, Blocked, Failed) without duplicating logic in each handler.

The post-run `.log` file (`WriteTaskLog`) continues to be written *before* the outcome handler — its destination is `Folders.Logs`, which is independent of the pending/destination move sequence.

### Why not move the live.log inside each outcome handler?
`HandleDoneTaskOutcome`, `HandleBlockedTaskOutcome`, and `HandleFailedTaskOutcome` already each call `moveTask.Move(...)` for the `.md` file. Adding a second move there would duplicate the same line in three places for behavior that does not vary by outcome. One call site in `ProcessTask` is the right shape per [naming-and-structure.md](.claude/rules/csharp/naming-and-structure.md) — match the abstraction level of the existing code.

### Why not do this inside `MoveTask`?
`MoveTask` has one job: move a single file by source/destination. Hooking sibling-file logic inside it would violate the one-responsibility rule. Keep `MoveTask` as-is and put the sibling logic in `MoveLiveLog`.

### Folder creation
`MoveFile` is a thin wrapper over `File.Move` and does not create the destination directory. Confirm during implementation whether `Folders.Logs` is created as part of repository setup ([CodeWorker/Commands/Setup/SetupRepository.cs](CodeWorker/Commands/Setup/SetupRepository.cs)) — if not, add it there. Repositories that were set up before this change must also work; either:
- Create the directory lazily inside `MoveLiveLog.Move` (one `Directory.CreateDirectory` call before `IMoveFile.Move`), or
- Add a small first-run path in `ValidateRepository` / `SetupRepository` that ensures the configured logs folder exists.

Pick the simpler option that matches how `Pending`/`Blocked`/`Failed` are currently created. Do not introduce a new abstraction for directory creation if the project already has one — match existing patterns.

## TDD Plan

Tests live under `CodeWorker.Tests` mirroring source folder structure. Use `xUnit` + `FakeItEasy` + `FluentAssertions`, matching existing tests. Write all tests **before** implementation.

### `MoveLiveLogTests` (new file)
1. `MoveTheLiveLogFromPendingToTheLogsFolder` — assert `IMoveFile.Move(sourcePath, destinationPath)` is called with `Folders.Pending\<base>.live.log` and `Folders.Logs\<base>.live.log`.
2. `DoNotMoveWhenTheLiveLogDoesNotExist` — `IFileSystemTools.FileExists` returns false; assert `IMoveFile.Move` is **never** called.
3. `LogThatThereWasNoLiveLogToMoveWhenSourceMissing` — single `ILogger.Information` call with the task name.
4. `LogThatTheLiveLogIsBeingMovedWhenSourceExists` — single `ILogger.Information` call with task name and destination path.
5. `DeriveTheBaseFileNameFromTheTaskName` — task name `"05-foo.md"` → log filename `"05-foo.live.log"`. Pin the task name explicitly.
6. `UseTheConfiguredLogsFolderFromTheContext` — change `context.Folders.Logs` between tests via `ReturnsLazily` and confirm the destination tracks it.

### `WriteTaskLogTests` (existing file)
- Update tests 1–3 (one per outcome that asserted the destination was `Folders.Done`/`Blocked`/`Failed`) to assert `Folders.Logs` instead. The destination should now be the same regardless of outcome.
- **Delete** the test that asserts `ArgumentOutOfRangeException` for an unhandled outcome — the switch is being removed.
- All other tests (5–15 from the original plan: log body composition) stay green unchanged because the body composition does not depend on the destination folder.

### `BuildTaskFoldersTests`
- Add `PopulateLogsFolderFromLogsFolderSetting` — confirms `Folders.Logs` equals `Path.Combine(repositoryPath, tasks.LogsFolder)`.
- Existing tests stay green.

### `ProcessTaskTests`
- `CallMoveLiveLogAfterTheOutcomeHandlerForDoneTasks`
- `CallMoveLiveLogAfterTheOutcomeHandlerForBlockedTasks`
- `CallMoveLiveLogAfterTheOutcomeHandlerForFailedTasks`
- `MoveLiveLogReceivesTheSameContextAndTaskAsTheOutcomeHandler` — assert argument equality (or pass-through).
- Use FakeItEasy `MustHaveHappenedOnceExactly().Then(...)` to assert ordering: outcome handler runs **before** `IMoveLiveLog.Move`.

### `LoadRepoSettingsTests` / settings deserialization tests
- `LoadLogsFolderFromTheJson` — exercise the JSON round-trip on a sample settings file that includes `LogsFolder`.
- Existing tests stay green.

### Integration / setup tests
- Any test that asserts on the contents of `defaultSettings.json` (e.g. setup snapshot tests) must be updated to include the new `LogsFolder` line.

Use `Faker.Create<...>()` for unrelated fields; pin the task name, the source/destination folders, and the booleans the assertion reads.

## Implementation Order

**Phase 1 — Settings plumbing**
1. Add `LogsFolder` to `TaskSettings` and `defaultSettings.json`.
2. Add `Logs` to `TaskFolders`. Update `BuildTaskFolders.Build`. Write/update `BuildTaskFoldersTests`. Green.
3. Update any setup snapshot / settings deserialization tests so the build is green again.

**Phase 2 — Redirect post-run `.log` to logs folder**
4. Update `WriteTaskLogTests` (per the bullet list above) — failing.
5. Replace the switch in `WriteTaskLog.Write` with `var destinationFolder = context.Folders.Logs;`. Green.
6. Confirm `dotnet test` is green before moving on.

**Phase 3 — Move the `.live.log` from pending**
7. Write all `MoveLiveLogTests` (1–6 above). They fail to compile.
8. Add `IMoveLiveLog` + `MoveLiveLog` (inline interface in same file). Green.
9. Write the new `ProcessTaskTests` for the call site. Failing.
10. Inject `IMoveLiveLog` into `ProcessTask` via the primary constructor; call `moveLiveLog.Move(context, task)` after `outcomeHandlerFactory.For(outcome).Handle(...)`. Green.

**Phase 4 — Folder creation**
11. Verify `tasks/logs/` is created at setup. If not, add it to whichever class currently creates `pending/`/`blocked/`/`failed/` — match the existing pattern. If existing repos must be migrated lazily, add a single `Directory.CreateDirectory` inside `MoveLiveLog.Move` (or a paired helper that already exists in this codebase — check before inventing one). Add a test that asserts the directory exists after `Move` runs against a clean filesystem fake.

**Phase 5 — Finish**
12. Confirm Autofac resolution: one implementation each of `IMoveLiveLog`. Per [types-and-di.md](.claude/rules/csharp/types-and-di.md), no module entry is required when there's a single implementation in the container. Run integration-level tests.
13. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`.

## Constraints

- Do not change [ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs) or [RunProcess.cs](CodeWorker/Process/RunProcess.cs). The live log continues to be written next to the in-flight task file in `tasks/pending/`. We move it after the fact.
- Do not change [LogTaskResult.cs](CodeWorker/Commands/Run/LogTaskResult.cs). The aggregate log is independent of this work.
- Do not change [MoveTask.cs](CodeWorker/Commands/Run/MoveTask.cs) or [MoveFile.cs](CodeWorker/FileSystem/MoveFile.cs).
- Do not introduce per-outcome logs subfolders (e.g. `done/logs`, `blocked/logs`). One configured `LogsFolder` covers all outcomes. Operators who want per-outcome separation can keep the existing layout by setting `LogsFolder` to e.g. `tasks/done` — they get the same effective layout as today, but only for Done; the same per-outcome split is not supported and not requested.
- Follow every rule in `.claude/rules/csharp/` — no exceptions. In particular: primary constructors, block-body methods, file-scoped namespaces, single class per file, inline interface in the same file when there is exactly one consumer, `Async` suffix only when overload disambiguation requires it, no `ConfigureAwait(false)`.

## Acceptance Criteria

After a single run that processes one task to a Done outcome:
- `tasks/pending/` is empty.
- `tasks/done/<task>.md` exists.
- `tasks/logs/<task>.log` exists with the existing log body shape.
- `tasks/logs/<task>.live.log` exists and is identical in content to what was previously left in `tasks/pending/`.
- `tasks/done/<task>.log` does **not** exist (no longer next to the task file).

The same pair of files appears in `tasks/logs/` for Blocked and Failed outcomes. The `.md` file moves to its outcome folder as before.

Setting `LogsFolder` to `"tasks/done/logs"` in `tasks/settings.json` redirects both `.log` and `.live.log` to that folder for **all** outcomes — the setting is a single switch, not per-outcome.

`tasks/run-history.jsonl`, `runs.jsonl`, and `CodeWorker.log` continue to be written exactly as before.

A task whose `.live.log` was never produced (e.g. open-for-write failed) finishes without throwing — the move is skipped silently and a single `Information` log line is emitted.

Build:
- `dotnet build`, `dotnet test`, `dotnet format` all clean. No new compiler warnings.

## Verification

Checklist carried forward verbatim from the root [task.md](task.md):

- [ ] Tests written before implementation (TDD)
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] Must follow all rules `.claude\rules\csharp` no exceptions
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] `dotnet build` to apply CSharpier changes
- [ ] Report results before finishing
