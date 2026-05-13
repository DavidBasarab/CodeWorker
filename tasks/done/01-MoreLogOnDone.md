# More Log On Done

## Objective
When a task finishes, write a **per-task `.log` file** that is named after the task and contains the full Claude output plus every piece of metadata we have about the run. This is a new artifact — it does not replace the existing aggregate [CodeWorker/Commands/Run/LogTaskResult.cs](CodeWorker/Commands/Run/LogTaskResult.cs), which appends a summary line block to a shared `CodeWorker.log` per repository.

Today the runner only appends an entry to `CodeWorker.log`. A reviewer who wants to see exactly what Claude produced for a single task has to grep through that shared file. A per-task log makes the Claude transcript and run metadata available as a dedicated, greppable artifact next to the task file itself.

## Scope
- [CodeWorker/Commands/Run/LogTaskResult.cs](CodeWorker/Commands/Run/LogTaskResult.cs) — existing aggregate logger, unchanged
- [CodeWorker/Commands/Run/ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) — calling site
- [CodeWorker/Commands/Run/TaskExecution.cs](CodeWorker/Commands/Run/TaskExecution.cs) — carries the `ProcessResult` we need to log
- [CodeWorker/Commands/Run/TaskFolders.cs](CodeWorker/Commands/Run/TaskFolders.cs) — provides `Done`, `Blocked`, `Failed` destinations
- [CodeWorker/Settings/RepoSettings.cs](CodeWorker/Settings/RepoSettings.cs) — where the `LogResults` toggle lives
- [CodeWorker/History/RecordRunHistory.cs](CodeWorker/History/RecordRunHistory.cs) — existing global history, unchanged
- [CodeWorker/History/RunHistoryEntry.cs](CodeWorker/History/RunHistoryEntry.cs) — existing global entry type, unchanged
- [CodeWorker/Commands/Run/RunGitWorkflow.cs](CodeWorker/Commands/Run/RunGitWorkflow.cs) / [CodeWorker/Git/CommitChanges.cs](CodeWorker/Git/CommitChanges.cs) — return the new commit hash so it can flow into the history entry
- New classes + tests:
  - `WriteTaskLog` / `WriteTaskLogTests`
  - `WriteFile` (no test — thin `File.WriteAllTextAsync` wrapper, `[ExcludeFromCodeCoverage]`)
  - `RecordRepositoryRunHistory` / `RecordRepositoryRunHistoryTests`
  - `RepositoryRunHistoryEntry` (POCO, no test)

## Design

### New single-purpose class
Add `CodeWorker/Commands/Run/WriteTaskLog.cs` with an interface declared in the same file (one consumer — `ProcessTask` — so the inline interface pattern from `naming-and-structure.md` applies):

```csharp
public interface IWriteTaskLog
{
    Task Write(TaskExecutionContext context, TaskExecution task, TaskOutcome outcome);
}

public class WriteTaskLog(IWriteFile writeFile, ILogger logger) : IWriteTaskLog
{
    public async Task Write(TaskExecutionContext context, TaskExecution task, TaskOutcome outcome)
    {
        // build path, compose content, write
    }
}
```

Responsibility is narrow and testable: given a context + task + outcome, compose a log body and write it to the correct destination path. No branching for outcome type beyond path selection and a header line.

### A new `IWriteFile` abstraction
`IAppendFile` exists; there is no `IWriteFile` yet. The per-task log should be written fresh (one task = one log file), not appended. Add a sibling class:

```csharp
public interface IWriteFile
{
    Task Write(string filePath, string content);
}

[ExcludeFromCodeCoverage(Justification = "Direct wrapper over File.WriteAllTextAsync — no business logic.")]
public class WriteFile : IWriteFile
{
    public async Task Write(string filePath, string content)
    {
        await File.WriteAllTextAsync(filePath, content);
    }
}
```

This keeps the abstraction symmetry with `AppendFile`/`IAppendFile` and keeps `WriteTaskLog` fully fakeable.

### Where the log file lives
Place the log file **next to the task file in its final destination folder**, using the base name of the task + `.log`:

- Done:    `tasks/done/05-refactor-run-process.log`
- Blocked: `tasks/blocked/02-missing-dep.log`
- Failed:  `tasks/failed/03-broken-tests.log`

Rationale: the task file itself moves to the destination folder (see [HandleDoneTaskOutcome.cs](CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs)). Co-locating the log with the task file keeps the run's artifacts together and makes grep-by-folder trivial. This also avoids inventing a new `tasks/logs/` folder that would not be discoverable from the existing folder conventions in the README.

### What the log contains
```
Task:           05-refactor-run-process.md
Repository:     C:\Projects\my-api
Outcome:        Done
Timestamp:      2026-04-23 17:32:14
Exit Code:      0
Timed Out:      false
Failed To Start:false
Reference Files: CLAUDE.md, tasks/reference/architecture.md

----- Claude Output -----
<full OutputLines joined by newline>

----- Claude Errors -----
<full ErrorLines joined by newline>
```

Full Claude transcript — no truncation. These files are intended to be inspected, so readability beats brevity.

### Where it gets called
Invoke `WriteTaskLog.Write` from [ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) **after** `classifyTaskResult.Classify(...)` returns and **before** the outcome handler moves the task file out of `tasks/pending/`. At that moment we know the outcome and the target folder, but the file has not yet moved — so we compose the log path using the outcome → destination folder mapping rather than the file's current location.

Gated by the same `RepoSettings.LogResults` toggle as the existing aggregate log — if logging is disabled, neither the aggregate nor the per-task log is written.

### Why not just extend `LogTaskResult`?
`LogTaskResult` already has one clear job: append to the aggregate log. Mixing per-task file writing into it would violate the one-responsibility rule. A separate class is the right shape per [naming-and-structure.md](.claude/rules/csharp/naming-and-structure.md).

## TDD Plan

All tests live in `CodeWorker.Tests/Commands/Run/WriteTaskLogTests.cs`, namespace `Testing.FatCat.CodeWorker.Commands.Run`. Use `xUnit` + `FakeItEasy` + `FluentAssertions`, matching [CodeWorkerApplicationTests.cs](CodeWorker.Tests/CodeWorkerApplicationTests.cs).

Tests to write **before any implementation**:

1. `WriteTheLogFileToTheDoneFolderForADoneOutcome` — asserts the path passed to `IWriteFile.Write` is `<Folders.Done>/<taskNameWithoutExtension>.log`.
2. `WriteTheLogFileToTheBlockedFolderForABlockedOutcome` — same, using `Folders.Blocked`.
3. `WriteTheLogFileToTheFailedFolderForAFailedOutcome` — same, using `Folders.Failed`.
4. `ThrowArgumentOutOfRangeForAnUnhandledOutcome` — verifies the `switch` expression's discard arm.
5. `IncludeTheTaskNameInTheLogBody`
6. `IncludeTheRepositoryPathInTheLogBody`
7. `IncludeTheOutcomeInTheLogBody`
8. `IncludeTheExitCodeInTheLogBody`
9. `IncludeTheTimedOutFlagInTheLogBody`
10. `IncludeTheFailedToStartFlagInTheLogBody`
11. `IncludeAllClaudeOutputLinesInTheLogBody`
12. `IncludeAllClaudeErrorLinesInTheLogBody`
13. `IncludeTheReferenceFileNamesInTheLogBody`
14. `ShowNoneWhenThereAreNoReferenceFiles`
15. `LogThatWeAreWritingTheTaskLog` — single `ILogger` call at `Information` with task name and path.

Use `Faker.Create<ProcessResult>()` etc. where the exact value does not matter. Pin task name, folder paths, and the fields the specific assertion reads.

After `WriteTaskLogTests` is green, extend `ProcessTaskTests` (new file if one does not already exist under that name):

16. `CallWriteTaskLogWhenLogResultsIsEnabled`
17. `DoNotCallWriteTaskLogWhenLogResultsIsDisabled`
18. `CallWriteTaskLogWithTheClassifiedOutcome`
19. `CallWriteTaskLogBeforeTheOutcomeHandlerRuns` — ordering assertion via `MustHaveHappenedOnceExactly().Then(...)`.

Tests for `RecordRepositoryRunHistoryTests` (namespace `Testing.FatCat.CodeWorker.History`):

20. `WriteToTheRepositoryTasksFolder` — path is `<repo>/tasks/run-history.jsonl`.
21. `AppendOneJsonLinePerEntry` — verifies the written content ends with `\n` and contains a single serialized object (no indentation).
22. `IncludeTheTaskName`
23. `IncludeTheOutcome`
24. `IncludeTheTimestamp`
25. `IncludeTheExitCode`
26. `IncludeTheDurationMs`
27. `IncludeTheCommitHashWhenProvided`
28. `OmitTheCommitHashWhenNull` — JSON should serialize `null`, or the field should be absent, depending on `JsonSerializerOptions`. Pick one and assert on it.

Additional `ProcessTaskTests`:

29. `RecordTheRepositoryRunHistoryForDoneTasks`
30. `RecordTheRepositoryRunHistoryForBlockedTasks`
31. `RecordTheRepositoryRunHistoryForFailedTasks`
32. `DoNotRecordTheRepositoryRunHistoryForPendingTasks`
33. `PassTheElapsedDurationToRepositoryHistory` — assert `DurationMs > 0` and matches the measured run.
34. `PassTheCommitHashToRepositoryHistoryWhenTheGitWorkflowReturnsOne`

## Implementation Order

**Phase 1 — Per-task `.log` file**

1. Write all `WriteTaskLogTests` (1–15) — they fail to compile (class does not exist).
2. Add `IWriteFile` + `WriteFile` (trivial, `[ExcludeFromCodeCoverage]`).
3. Add `WriteTaskLog` with enough shape to compile the tests — methods return without doing work.
4. Fill in path resolution (`switch` expression on `TaskOutcome`). Tests 1–4 go green.
5. Fill in body composition. Tests 5–15 go green.
6. Write `ProcessTask` tests 16–19 (they fail — no dependency wired yet).
7. Add `IWriteTaskLog` to `ProcessTask`'s primary constructor and call it from `Run` after `Classify`. Tests 16–19 go green.

**Phase 2 — Per-repository run-history file**

8. Add `RepositoryRunHistoryEntry` POCO.
9. Write `RecordRepositoryRunHistoryTests` (20–28) — fail to compile.
10. Add `RecordRepositoryRunHistory` + `IRecordRepositoryRunHistory`. Tests 20–28 go green.
11. Write `ProcessTask` tests 29–33 (duration + outcome gating). These force a `Stopwatch` to wrap `runClaude.Run(...)`. Wire up and go green.
12. Plumb the commit hash from [CommitChanges.cs](CodeWorker/Git/CommitChanges.cs) through [RunGitWorkflow.cs](CodeWorker/Commands/Run/RunGitWorkflow.cs) back to `ProcessTask`. This is its own TDD cycle — start with `CommitChangesTests` returning the hash, then `RunGitWorkflowTests` propagating it, then `ProcessTaskTests` test 34.

**Phase 3 — Finish**

13. Verify Autofac resolution: one implementation each of `IWriteTaskLog`, `IWriteFile`, and `IRecordRepositoryRunHistory`, so no `CodeWorkerModule` entry is required per [types-and-di.md](.claude/rules/csharp/types-and-di.md). Run the existing integration-level tests to confirm.
14. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`.

If phase 2 step 12 proves larger than expected, stop there, ship without `CommitHash`, and open a follow-up task. Do not pause phases 1 and the rest of phase 2 for it.

## Decisions (confirmed)

- **Blocked and failed outcomes get the same per-task `.log` file** as Done — same format, same rules, just dropped into `tasks/blocked/` or `tasks/failed/` next to the moved task file. The Claude transcript is most valuable on non-success runs.
- **Pending (deferred) tasks get no log.** Pending means the task has not actually run yet, so there is no `ProcessResult` to record.
- **The aggregate `CodeWorker.log` stays.** It serves the at-a-glance timeline view; the per-task log serves the drill-down view. Both continue to be gated by `RepoSettings.LogResults`.

## Per-Repository Run History File

In addition to the per-task log, write a **long-term run history file inside the repository's `tasks/` folder** so run history travels with the repo (checked in, greppable, survives reinstalls). This is separate from the existing global `runs.jsonl` in `AppContext.BaseDirectory` (see [RecordRunHistory.cs](CodeWorker/History/RecordRunHistory.cs)) which backs the `info` command across all tracked repos.

### File
- Location: `tasks/run-history.jsonl`
- Format: JSON Lines — one entry per line, same shape as the global file plus a couple of fields that are specific to a single repo.
- Lifetime: append-only, never rotated by the runner. Operators can prune it manually.

### Entry shape
Introduce `RepositoryRunHistoryEntry` alongside the existing [RunHistoryEntry.cs](CodeWorker/History/RunHistoryEntry.cs). Fields:

```
Timestamp   — DateTime
TaskName    — string  (file name, with extension, e.g. 05-refactor-run-process.md)
Outcome     — TaskOutcome (Done | Blocked | Failed — pending is never written)
ExitCode    — int
DurationMs  — long
CommitHash  — string?  (populated only on Done when Git.CommitAfterEachTask produced a commit; null otherwise)
```

The global `runs.jsonl` keeps its existing `RunHistoryEntry` shape (`Repository`, `TaskName`, `Timestamp`, `Success`). The new repo-level entry is its own type — repo is redundant inside a repo-local file, and we want the richer outcome/duration/commit info.

### New class
`CodeWorker/History/RecordRepositoryRunHistory.cs` with an inline interface:

```csharp
public interface IRecordRepositoryRunHistory
{
    Task Record(string repositoryPath, RepositoryRunHistoryEntry entry);
}

public class RecordRepositoryRunHistory(IAppendFile appendFile, ILogger logger) : IRecordRepositoryRunHistory
{
    public async Task Record(string repositoryPath, RepositoryRunHistoryEntry entry) { ... }
}
```

Writes to `Path.Combine(repositoryPath, "tasks", "run-history.jsonl")` using `IAppendFile`. A single `JsonSerializerOptions` instance, lowercase, no indentation (one line per entry).

### Call site
Invoke immediately after the existing `recordRunHistory.Record(...)` call in [ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) so both history files are updated in the same step. The existing call keeps writing the global file — we are not replacing it, we are adding a parallel repo-local file.

Skipped when the outcome is Pending (same rule as per-task log).

### Duration
Wrap the `runClaude.Run(...)` call in a `Stopwatch` in [ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) so `DurationMs` is available on the entry. No new abstraction needed — `Stopwatch` is a value type with no external state.

### Commit hash
On Done, the git workflow in [RunGitWorkflow.cs](CodeWorker/Commands/Run/RunGitWorkflow.cs) and [CommitChanges.cs](CodeWorker/Git/CommitChanges.cs) creates the commit. Thread the resulting hash back through `TaskProcessingDecision` (or a new field on `TaskExecution`) so the history record can include it. If `CommitAfterEachTask` is false or the outcome is not Done, leave `CommitHash = null`.

This is the most invasive part of the plan because it touches the git workflow return shape. If it becomes too large a change, split it: ship the history file **without** `CommitHash` first, then add the hash in a follow-up task. Acceptable because the hash is nice-to-have, not load-bearing.

## Constraints

- Do not change public APIs of unrelated classes.
- Do not modify [LogTaskResult.cs](CodeWorker/Commands/Run/LogTaskResult.cs) — new behavior goes in the new class.
- Preserve the existing `LogResults` gating semantics.
- Follow every rule in `.claude/rules/csharp/` — no exceptions. Particularly: primary constructors, block-body methods, `switch` expressions with discard arms, single class per file, `IWrite…` interface naming describing capability.

## Acceptance Criteria

Per-task `.log` file:
- A completed task in `tasks/done/` has a corresponding `.log` file next to it with the same base name.
- A blocked task in `tasks/blocked/` has the same.
- A failed task in `tasks/failed/` has the same.
- A pending task in `tasks/pending/` has **no** `.log` file written.
- The log contains the task name, repo path, outcome, timestamp, exit code, timed-out flag, failed-to-start flag, reference file list, full output, full errors.
- Disabling `LogResults` in `tasks/settings.json` suppresses both the aggregate `CodeWorker.log` and the new per-task `.log`.
- The existing `CodeWorker.log` aggregate still works.

Per-repository run-history file:
- After a run, `tasks/run-history.jsonl` exists in the repository root (created if missing).
- Each processed task (Done, Blocked, or Failed) appends exactly one JSON line to that file.
- Pending outcomes append nothing.
- Each line contains `Timestamp`, `TaskName`, `Outcome`, `ExitCode`, `DurationMs`, and `CommitHash` (nullable).
- `DurationMs` reflects the wall-clock duration of the Claude invocation.
- `CommitHash` is populated for Done tasks that produced a commit, null otherwise.
- The existing global `runs.jsonl` in `AppContext.BaseDirectory` still works; the `info` command is unaffected.

Build:
- `dotnet build`, `dotnet test`, `dotnet format` all clean.

## Verification

Checklist carried forward verbatim from the root `task.md`:

- [ ] Tests written before implementation (TDD)
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] Must follow all rules `.claude\rules\csharp` no exceptions
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] `dotnet build` to apply CSharpier changes
- [ ] Report results before finishing
