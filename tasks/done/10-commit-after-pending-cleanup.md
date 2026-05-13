# Commit Only After Pending Sibling Files Are Cleared

## Objective

The git auto-commit produced by the runner currently includes wrapper-artifact files left behind in `tasks/pending/`. This was visible in the recent pair of commits:

- [`b6265c0`](https://example/commit/b6265c0) — runner's auto-commit `🤖 09-task-template-readme`. It correctly moved `tasks/todo/09-task-template-readme.md` → `tasks/done/09-task-template-readme.md`, but it **also added** these orphans in `tasks/pending/`:
  - `09-task-template-readme.prompt.txt`
  - `09-task-template-readme.stderr.log`
  - `09-task-template-readme.transcript.jsonl`
  - `09-task-template-readme.wrapper.log`
  - `09-task-template-readme.wrapper.pid`
  - `09-task-template-readme.wrapper.started`
- [`973d0d7`](https://example/commit/973d0d7) — manual follow-up commit that the user had to make to delete those orphans.

The runner's commit should be the *only* commit. After it runs, `tasks/pending/` must be empty for that task — no second clean-up commit should ever be necessary.

Two root causes:

1. **The git commit is invoked from inside [HandleDoneTaskOutcome.Handle](CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs)**, which is called from [ProcessTask.Run](CodeWorker/Commands/Run/ProcessTask.cs) **before** `moveLiveLog.Move(context, task)` runs. So at commit time, every sibling artifact that `MoveLiveLog` would relocate or delete is still sitting in `tasks/pending/`.
2. **[MoveLiveLog](CodeWorker/Commands/Run/MoveLiveLog.cs) does not handle every sibling produced by the runner.** Today it covers `.live.log`, `.transcript.jsonl`, `.stderr.log` (moved to `logs/`) and `.prompt.txt`, `.done`, `.wrapper.pid` (deleted). It misses `.wrapper.log`, `.wrapper.started`, and `.claude-args.txt`. Those are exactly the files that leaked into `b6265c0`.

This task fixes both.

## Scope

- [CodeWorker/Commands/Run/ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) — invoke git workflow here, after the outcome handler **and** after `moveLiveLog.Move(...)`.
- [CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs](CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs) — drop the `IRunGitWorkflow` dependency and call. Handler only moves the `.md`.
- [CodeWorker/Commands/Run/MoveLiveLog.cs](CodeWorker/Commands/Run/MoveLiveLog.cs) — extend the `Suffixes` and `CleanupSuffixes` arrays so every sibling produced in pending/ is either moved to `logs/` or deleted.
- Updated tests:
  - `ProcessTaskTests` — assert the git workflow runs **after** `IMoveLiveLog.Move`, and only for `TaskOutcome.Done`. Use FakeItEasy `MustHaveHappenedOnceExactly().Then(...)` ordering.
  - `HandleDoneTaskOutcomeTests` — drop assertions about `IRunGitWorkflow`. The handler must no longer take `IRunGitWorkflow`.
  - `MoveLiveLogTests` — add coverage for the three previously-missed siblings (`.wrapper.log`, `.wrapper.started`, `.claude-args.txt`). Pin which suffix list each one belongs to.
- Out of scope:
  - [RunGitWorkflow.cs](CodeWorker/Commands/Run/RunGitWorkflow.cs) — its body is correct. We change *where* it's invoked from, not what it does.
  - The push step inside `RunGitWorkflow` — unchanged.
  - [RecoverPendingTasks.cs](CodeWorker/Claude/RecoverPendingTasks.cs) — its `ArchiveTranscript` already handles a similar set of files; do **not** unify the two paths in this task. They run in different contexts (recovery vs. live finish). A follow-up task can deduplicate later.
  - Blocked / Failed outcomes — they currently never commit. That stays the same. Git workflow is invoked only when outcome is `Done`, mirroring the previous wiring.

## Design

### Move git workflow out of the outcome handler

Today's chain inside [ProcessTask.Run](CodeWorker/Commands/Run/ProcessTask.cs):

```text
classify outcome
WriteTaskLogIfEnabled
RecordHistory + RecordRepositoryHistory
outcomeHandler.Handle
    └── HandleDoneTaskOutcome
        ├── moveTask.Move(.md → done/)
        └── runGitWorkflow.Run     ← COMMIT happens here, while pending/ still holds wrapper artifacts
moveLiveLog.Move                    ← cleans up pending/ AFTER the commit (too late)
```

Required chain:

```text
classify outcome
WriteTaskLogIfEnabled
RecordHistory + RecordRepositoryHistory
outcomeHandler.Handle               ← only moves the .md to its outcome folder
moveLiveLog.Move                    ← clears every sibling out of pending/
RunGitWorkflowIfDone                ← COMMIT now only sees the .md move + logs/ files
return decision
```

Concretely:

1. Remove `IRunGitWorkflow` and the `runGitWorkflow.Run(...)` call from [HandleDoneTaskOutcome](CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs). The handler returns `TaskProcessingDecision.Continue` after the `.md` move.
2. Inject `IRunGitWorkflow` into [ProcessTask](CodeWorker/Commands/Run/ProcessTask.cs) via the primary constructor.
3. After `moveLiveLog.Move(context, task)`, run:

   ```csharp
   var decision = await outcomeHandlerFactory.For(outcome).Handle(context, task);

   moveLiveLog.Move(context, task);

   if (outcome == TaskOutcome.Done)
   {
       var gitDecision = await runGitWorkflow.Run(context, task);
       if (gitDecision == TaskProcessingDecision.Stop)
       {
           return TaskProcessingDecision.Stop;
       }
   }

   return decision;
   ```

   The `outcome == TaskOutcome.Done` guard preserves today's behaviour: blocked / failed runs never auto-commit, because the prior wiring only invoked `RunGitWorkflow` from the Done handler.

### Cover every sibling produced by the runner

[BuildTranscriptPaths](CodeWorker/Claude/BuildTranscriptPaths.cs) is the source of truth for what siblings exist. Today it produces:

| Suffix              | Today's `MoveLiveLog` behaviour | Required behaviour |
|---------------------|--------------------------------|--------------------|
| `.live.log`         | move → `logs/`                 | move → `logs/`     |
| `.transcript.jsonl` | move → `logs/`                 | move → `logs/`     |
| `.stderr.log`       | move → `logs/`                 | move → `logs/`     |
| `.wrapper.log`      | **leaks into commit**          | move → `logs/`     |
| `.wrapper.started`  | **leaks into commit**          | delete             |
| `.wrapper.pid`      | delete                         | delete             |
| `.done`             | delete                         | delete             |
| `.prompt.txt`       | delete                         | delete             |
| `.claude-args.txt`  | **leaks into commit**          | delete             |

`.wrapper.log` is genuinely useful (the wrapper script's own log) — keep it alongside the other moved logs in `logs/`.
`.wrapper.started` and `.claude-args.txt` are sentinels / one-shot inputs with no value after the run — delete them.

In [MoveLiveLog.cs](CodeWorker/Commands/Run/MoveLiveLog.cs):

```csharp
private static readonly string[] Suffixes =
[
    ".live.log",
    ".transcript.jsonl",
    ".stderr.log",
    ".wrapper.log",
];

private static readonly string[] CleanupSuffixes =
[
    ".prompt.txt",
    ".done",
    ".wrapper.pid",
    ".wrapper.started",
    ".claude-args.txt",
];
```

The two existing helpers (`MoveOne`, `DeleteOne`) handle the new entries without further change.

### Why not call `runGitWorkflow` from inside `MoveLiveLog`?

`MoveLiveLog` is a single-responsibility filesystem operator. Hooking the git workflow into it would couple two unrelated concerns. The orchestrator that already knows about both (`ProcessTask`) is the right call site.

### Why not call `runGitWorkflow` from each outcome handler after a sibling-cleanup step?

Two reasons:
1. It would duplicate the cleanup-then-commit sequence in three handlers (only Done currently commits, but the *cleanup* applies to all three).
2. The post-handler ordering in `ProcessTask` already exists for `MoveLiveLog`. Adding `RunGitWorkflow` next to it keeps both end-of-task concerns in one place. This matches the abstraction level already chosen in [naming-and-structure.md](.claude/rules/csharp/naming-and-structure.md).

### Will the commit pick up `logs/<task>.log` and `logs/<task>.live.log`?

Yes — and that is the desired behaviour. The `logs/` folder is part of the repo (see [task 08](tasks/done/08-relocate-task-logs.md)). Once `WriteTaskLog` and `MoveLiveLog` have run, the new files in `logs/` are exactly the artifacts the operator wants captured in the commit. The commit message format from [GitSettings.CommitMessagePrefix](CodeWorker/Settings/GitSettings.cs) is unchanged.

### Will the commit fire when there is nothing to commit?

The current [CommitChanges](CodeWorker/Git/CommitChanges.cs) already handles the no-op case via git's own exit code; this task does not change that contract. If `git commit` returns non-zero because there is nothing staged, behavior is the same as before this change. If empty-commit behaviour needs hardening, that is a separate task — explicitly out of scope here.

## TDD Plan

Tests live under `CodeWorker.Tests` mirroring source folders. Use `xUnit` + `FakeItEasy` + `FluentAssertions`. Write tests **before** implementation.

### `MoveLiveLogTests` (existing file)

Add tests:
1. `MoveTheWrapperLogToTheLogsFolder` — assert `IMoveFile.Move(pending\<base>.wrapper.log, logs\<base>.wrapper.log)`.
2. `DeleteTheWrapperStartedSentinel` — assert `IFileSystemTools.DeleteFile(pending\<base>.wrapper.started)`.
3. `DeleteTheClaudeArgsFile` — assert `IFileSystemTools.DeleteFile(pending\<base>.claude-args.txt)`.

Existing tests for `.live.log`, `.transcript.jsonl`, `.stderr.log`, `.prompt.txt`, `.done`, `.wrapper.pid` stay green unchanged.

### `HandleDoneTaskOutcomeTests`

- Delete the test that asserts `IRunGitWorkflow.Run(context, task)` is called.
- Update the constructor-setup section to drop `IRunGitWorkflow` from the fakes list (the handler no longer takes it).
- Existing tests for the `.md` move stay green unchanged.

### `ProcessTaskTests`

- `RunGitWorkflowAfterMoveLiveLogForDoneTasks` — `IRunGitWorkflow.Run` is called and ordering is `outcomeHandler.Handle.MustHaveHappenedOnceExactly().Then(moveLiveLog.Move).Then(runGitWorkflow.Run)`.
- `DoNotRunGitWorkflowForBlockedTasks` — `IRunGitWorkflow.Run` is **never** called when outcome is Blocked.
- `DoNotRunGitWorkflowForFailedTasks` — `IRunGitWorkflow.Run` is **never** called when outcome is Failed.
- `ReturnStopWhenGitWorkflowReturnsStop` — `runGitWorkflow.Run` returns `Stop`; assert `ProcessTask.Run` returns `Stop`.
- `MoveLiveLogStillRunsBeforeGitWorkflowEvenWhenGitWorkflowFails` — order assertion holds when `runGitWorkflow.Run` returns `Stop`.

Pin `outcome` and `decision` explicitly per test; spread `Faker.Create<TaskExecutionContext>()` for unrelated state.

### `RunGitWorkflowTests`

- No new tests required. Behaviour is unchanged; only the call site moves.

## Implementation Order

**Phase 1 — Cover the missing siblings**
1. Add the three failing `MoveLiveLogTests` (wrapper.log, wrapper.started, claude-args.txt). Red.
2. Update `Suffixes` and `CleanupSuffixes` in [MoveLiveLog.cs](CodeWorker/Commands/Run/MoveLiveLog.cs). Green.
3. `dotnet test` — confirm green before moving on.

**Phase 2 — Move the commit out of the Done handler**
4. Update `HandleDoneTaskOutcomeTests` (delete the git-workflow assertion; drop `IRunGitWorkflow` from setup). Red.
5. Remove `IRunGitWorkflow` from [HandleDoneTaskOutcome](CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs)'s primary constructor; remove the `runGitWorkflow.Run(...)` call; return `TaskProcessingDecision.Continue` after the move. Green.
6. Add the new `ProcessTaskTests` (ordering + per-outcome guard + Stop propagation). Red.
7. Inject `IRunGitWorkflow` into [ProcessTask](CodeWorker/Commands/Run/ProcessTask.cs); call `runGitWorkflow.Run(context, task)` after `moveLiveLog.Move(...)` only when `outcome == TaskOutcome.Done`. Propagate `Stop`. Green.
8. `dotnet test` — confirm green.

**Phase 3 — Finish**
9. Confirm Autofac resolution: `IRunGitWorkflow` already has a single implementation; per [types-and-di.md](.claude/rules/csharp/types-and-di.md) no module entry is required. Verify the dependency graph still builds.
10. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`.

## Constraints

- Do not change [RunGitWorkflow.cs](CodeWorker/Commands/Run/RunGitWorkflow.cs) — only its call site moves.
- Do not change [BuildTranscriptPaths.cs](CodeWorker/Claude/BuildTranscriptPaths.cs). The list of siblings is fixed by what the runner already produces.
- Do not change [RecoverPendingTasks.cs](CodeWorker/Claude/RecoverPendingTasks.cs). Its `ArchiveTranscript` is for the recovery path, not the live-finish path. A future task can unify them.
- Do not introduce a new abstraction to choose "commit on Done only". A single `if (outcome == TaskOutcome.Done)` in `ProcessTask` is the right shape — it matches the existing `outcome == TaskOutcome.Done` check at [ProcessTask.cs:119](CodeWorker/Commands/Run/ProcessTask.cs).
- Follow every rule in `.claude/rules/csharp/` — no exceptions. In particular: primary constructors, block-body methods, file-scoped namespaces, single class per file, inline interface in the same file when there is exactly one consumer, no `Async` suffix unless overload disambiguation requires it, no `ConfigureAwait(false)`.

## Acceptance Criteria

After a single Done run:
- `tasks/pending/` is empty for that task — no `.prompt.txt`, `.transcript.jsonl`, `.stderr.log`, `.wrapper.log`, `.wrapper.pid`, `.wrapper.started`, `.claude-args.txt`, `.done`, or `.live.log` remain.
- `tasks/done/<task>.md` exists.
- `tasks/logs/<task>.log`, `tasks/logs/<task>.live.log`, `tasks/logs/<task>.transcript.jsonl`, `tasks/logs/<task>.stderr.log`, and `tasks/logs/<task>.wrapper.log` exist.
- The single auto-commit by the runner contains: the `.md` move into `tasks/done/`, plus the new files in `tasks/logs/`. **Nothing in `tasks/pending/`.**
- `git status` after the runner finishes is clean — no second manual commit is required.

For Blocked / Failed outcomes:
- `tasks/pending/` is empty for that task (same cleanup runs).
- The runner does **not** auto-commit. Behaviour matches today.

Build:
- `dotnet build`, `dotnet test`, `dotnet format` all clean. No new compiler warnings.

## Verification

- [ ] Tests written before implementation (TDD)
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] Must follow all rules `.claude\rules\csharp` no exceptions
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] `dotnet build` to apply CSharpier changes
- [ ] Manually run a task end-to-end and confirm `tasks/pending/` is empty and the runner's commit is the only commit needed
- [ ] Report results before finishing
