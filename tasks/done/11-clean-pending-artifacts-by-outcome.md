# Clean Pending Artifacts on Success, Preserve Them on Failure

## Objective

When a task finishes, every sibling artifact the runner produced in `tasks/pending/` must be dealt with by outcome:

- **Success (`TaskOutcome.Done`)** — delete all sibling artifacts from `tasks/pending/`. There is nothing left for an operator to investigate, and `tasks/run-history.jsonl` plus `tasks/logs/<task>.log` already record what happened.
- **Failure (`TaskOutcome.Failed`)** — move every sibling artifact to `tasks/failed/` next to the moved `.md`. The full set of wrapper output is exactly what an operator needs to debug a failure, so it should follow the task into `failed/`.
- **Blocked (`TaskOutcome.Blocked`)** — same treatment as Failed: move every sibling artifact alongside the `.md` in `tasks/blocked/`. Blocked is also a state that requires human inspection.

The current state of `tasks/pending/` after a recent run shows the problem this task fixes:

```
tasks/pending/
  09-task-template-readme.claude-args.txt
  09-task-template-readme.done
  09-task-template-readme.transcript.jsonl
  09-task-template-readme.wrapper.log
  09-task-template-readme.wrapper.started
```

After this task is complete, `tasks/pending/` must be **empty** for any task whose outcome has been classified — no matter the outcome.

## Relationship to task 10

[Task 10 — Commit Only After Pending Sibling Files Are Cleared](tasks/todo/10-commit-after-pending-cleanup.md) and this task touch the same call site (`ProcessTask.Run` after the outcome handler) and the same class (`MoveLiveLog`). Resolution between them:

- **Task 10's "move commit out of `HandleDoneTaskOutcome` into `ProcessTask`" still stands.** That is correct independently of how siblings are handled. Pending cleanup must run before the commit either way.
- **Task 10's exact MoveLiveLog suffix lists are superseded by this task.** The split this task uses (delete-on-Done, move-to-outcome-folder otherwise) replaces task 10's "always move some, always delete others" model. After this task ships, the outcome decides every sibling's fate uniformly — there is no per-suffix routing.
- If task 10 is implemented first, this task replaces the contents of `MoveLiveLog` (or supersedes it with a new class — see Design). If this task is implemented first, task 10's "extend the suffix lists" step becomes unnecessary; only its "move git workflow into ProcessTask" step remains.

Either ordering is fine. Implement them in whichever sequence the operator picks; the acceptance criteria are independent.

## Scope

- [CodeWorker/Commands/Run/MoveLiveLog.cs](CodeWorker/Commands/Run/MoveLiveLog.cs) — replace its current per-suffix routing with outcome-driven behaviour. Keep the class name and `IMoveLiveLog` interface to minimise call-site churn, but the body is rewritten. (See Design for naming alternatives.)
- [CodeWorker/Commands/Run/ProcessTask.cs](CodeWorker/Commands/Run/ProcessTask.cs) — pass the classified `TaskOutcome` into the cleanup call, so the cleanup can branch on it. The call site moves to *after* the outcome handler, same as today / task 10.
- [CodeWorker/Claude/BuildTranscriptPaths.cs](CodeWorker/Claude/BuildTranscriptPaths.cs) — **not changed**. Its `TranscriptPaths` already enumerates every sibling produced by the runner. Cleanup iterates the existing fields, not a hard-coded suffix list, so no new sibling can be silently missed in the future.
- New / updated tests:
  - `MoveLiveLogTests` (existing file) — replace the suffix-routing tests with outcome-routing tests (see TDD Plan).
  - `ProcessTaskTests` — assert that the outcome value is passed through correctly to the cleanup call, for all three outcomes.
- Out of scope:
  - The `.md` move itself — still owned by the outcome handlers.
  - The git workflow — owned by [task 10](tasks/todo/10-commit-after-pending-cleanup.md). After both ship, the order in `ProcessTask` is: outcome handler → pending cleanup (this task) → git workflow (task 10), Done only.
  - [RecoverPendingTasks.cs](CodeWorker/Claude/RecoverPendingTasks.cs) — its `ArchiveTranscript` runs in the recovery path on a separate process. Out of scope. A future task can unify them.
  - Any new logs-folder routing. Per-task `.log` and aggregate logs are unchanged. This task does not touch [WriteTaskLog.cs](CodeWorker/Commands/Run/WriteTaskLog.cs) or [LogTaskResult.cs](CodeWorker/Commands/Run/LogTaskResult.cs).

## Design

### Drive cleanup off the enumerated `TranscriptPaths`

[BuildTranscriptPaths](CodeWorker/Claude/BuildTranscriptPaths.cs) already produces the full set of sibling paths for a given `.md`:

```csharp
return new TranscriptPaths
{
    TaskName = taskName,
    PromptFile = ...,
    TranscriptFile = ...,
    StderrFile = ...,
    DoneSentinel = ...,
    PidFile = ...,
    LiveLogFile = ...,
    WrapperStartedFile = ...,
    WrapperLogFile = ...,
    ClaudeArgsFile = ...,
};
```

Cleanup iterates over those file paths — not a hard-coded suffix array — so any new sibling added to `TranscriptPaths` in the future is picked up automatically. This avoids the silent-omission bug that today's `MoveLiveLog` has (it only knows six of the nine suffixes).

### `IMoveLiveLog` becomes outcome-aware

Rename intent: today the class is "move the live log and a few siblings". After this task it is "clean up every pending sibling, by outcome". The class name `MoveLiveLog` is misleading after that. Two options:

1. **Rename to `CleanPendingArtifacts`** (preferred) and update the single consumer. The interface becomes `ICleanPendingArtifacts`.
2. Keep the existing `MoveLiveLog` name and just rewrite the body. Less churn, but the name lies about what the class does.

Pick option 1 unless there is a concrete reason not to. The class is single-purpose; renaming is a one-line consumer change.

The new contract:

```csharp
public interface ICleanPendingArtifacts
{
    void Clean(TaskExecutionContext context, TaskExecution task, TaskOutcome outcome);
}

public class CleanPendingArtifacts(
    IFileSystemTools fileSystemTools,
    IMoveFile moveFile,
    ITranscriptPaths transcriptPaths,
    ILogger logger
) : ICleanPendingArtifacts
{
    public void Clean(TaskExecutionContext context, TaskExecution task, TaskOutcome outcome)
    {
        var paths = transcriptPaths.For(task.PendingFilePath);
        var siblingFiles = EnumerateSiblings(paths);

        var destination = ResolveDestination(outcome, context.Folders);

        if (destination is null)
        {
            DeleteAll(siblingFiles, task.TaskName);
            return;
        }

        fileSystemTools.EnsureDirectory(destination);
        MoveAll(siblingFiles, destination, task.TaskName);
    }

    private static IReadOnlyList<string> EnumerateSiblings(TranscriptPaths paths)
    {
        return
        [
            paths.PromptFile,
            paths.TranscriptFile,
            paths.StderrFile,
            paths.DoneSentinel,
            paths.PidFile,
            paths.LiveLogFile,
            paths.WrapperStartedFile,
            paths.WrapperLogFile,
            paths.ClaudeArgsFile,
        ];
    }

    private string ResolveDestination(TaskOutcome outcome, TaskFolders folders)
    {
        return outcome switch
        {
            TaskOutcome.Done => null,
            TaskOutcome.Blocked => folders.Blocked,
            TaskOutcome.Failed => folders.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    private void DeleteAll(IReadOnlyList<string> sources, string taskName) { /* per-file: log+delete if exists */ }

    private void MoveAll(IReadOnlyList<string> sources, string destinationFolder, string taskName) { /* per-file: log+move if exists */ }
}
```

Rules baked into this design:

- **A null destination means "delete all"** — explicit, matches the switch above. Done is the only outcome that returns null.
- **A non-null destination means "move every existing sibling there"** — Blocked → `Folders.Blocked`, Failed → `Folders.Failed`. The `.md` itself is already in that folder by the time `Clean` runs, so the wrapper artifacts join it.
- **Missing siblings are silently skipped.** A task whose live log never opened, or whose done sentinel was deleted by something else, must not throw. Each per-file helper checks `FileExists` first.
- **The discard-arm `_ => throw new ArgumentOutOfRangeException(nameof(outcome))` is mandatory** per [naming-and-structure.md](.claude/rules/csharp/naming-and-structure.md) for switches on enums.

### Why not delete on Blocked too?

Blocked means the task explicitly reported it could not proceed without human input. The wrapper's output (transcript, stderr, claude args, live log) is the most useful evidence for the operator deciding what to unblock. Deleting it would make Blocked harder to investigate than it should be. Treat Blocked the same as Failed.

### Why not move on Done to a `done/` artifacts folder?

Done is the success path. A successful run's transcript, prompt, and wrapper log are not interesting after the fact — `tasks/run-history.jsonl` records the outcome, and the per-task `.log` (from [WriteTaskLog.cs](CodeWorker/Commands/Run/WriteTaskLog.cs)) records the summary. Keeping a copy of every transcript in `tasks/done/` for every successful task would balloon the repo with low-value data. Delete on success.

If a future operator wants the artifacts kept on Done, that is a separate task and should be gated by a setting; do not introduce that setting now.

### Where does this hook into `ProcessTask`?

Replace the existing `moveLiveLog.Move(context, task)` call in [ProcessTask.cs:79](CodeWorker/Commands/Run/ProcessTask.cs#L79) with:

```csharp
var decision = await outcomeHandlerFactory.For(outcome).Handle(context, task);

cleanPendingArtifacts.Clean(context, task, outcome);

return decision;
```

Two important properties:
1. The cleanup runs **after** the outcome handler — so the `.md` has already moved out of pending. Cleanup therefore only touches sibling artifacts, never the `.md`.
2. The cleanup runs for **every** outcome, not just Done. The cleanup decides per outcome whether to delete or relocate.

This makes `tasks/pending/` empty for that task in all three outcomes.

### Folder creation

`Folders.Blocked` and `Folders.Failed` are already created by repo setup ([SetupRepository.cs](CodeWorker/Commands/Setup/SetupRepository.cs)). The `EnsureDirectory(destination)` call in `Clean` is defensive — it costs nothing and protects pre-existing repos that may not have run setup since these folders were introduced. Match the pattern already used at the top of today's `MoveLiveLog`.

## TDD Plan

Tests live under `CodeWorker.Tests` mirroring source folders. Use `xUnit` + `FakeItEasy` + `FluentAssertions`. Write tests **before** implementation.

### `CleanPendingArtifactsTests` (new file, replaces `MoveLiveLogTests`)

If renaming, delete `MoveLiveLogTests.cs` after the new file is in place. If keeping the `MoveLiveLog` name, replace its body in place.

1. `DeleteEverySiblingFromPendingWhenOutcomeIsDone` — assert each path returned by `ITranscriptPaths.For(...)` (live log, transcript, stderr, prompt, done, pid, wrapper.started, wrapper.log, claude-args) is passed to `IFileSystemTools.DeleteFile`.
2. `DoNotMoveAnyFileWhenOutcomeIsDone` — assert `IMoveFile.Move` is **never** called.
3. `MoveEverySiblingToFailedFolderWhenOutcomeIsFailed` — assert each existing sibling is `IMoveFile.Move(source, Folders.Failed\<filename>)`.
4. `MoveEverySiblingToBlockedFolderWhenOutcomeIsBlocked` — symmetric to Failed.
5. `DoNotDeleteAnyFileWhenOutcomeIsFailed` — assert `IFileSystemTools.DeleteFile` is **never** called.
6. `DoNotDeleteAnyFileWhenOutcomeIsBlocked` — symmetric to Failed.
7. `SkipSiblingsThatDoNotExist` — `FileExists` returns false for half the siblings; assert neither `Move` nor `DeleteFile` is called for those, and the rest are processed normally. Run once per outcome.
8. `EnsureDestinationFolderExistsBeforeMove` — for Failed and Blocked, `IFileSystemTools.EnsureDirectory` is called with the destination *before* any `Move`. Use FakeItEasy `Then(...)` ordering.
9. `LogPerFileActionAtInformationLevel` — one `ILogger.Information` call per file actually deleted or moved. (Lightweight — verifies operator-visible logging exists; do not over-specify the message format.)
10. `ThrowArgumentOutOfRangeForUnknownOutcome` — pass an out-of-range `TaskOutcome` value and assert `ArgumentOutOfRangeException`. Mirrors the discard arm.
11. `IterateEverySiblingFromTranscriptPaths` — fake `ITranscriptPaths.For(...)` to return a `TranscriptPaths` with each field set to a unique sentinel string, and assert every one of those sentinels appears in either the `Move` or `DeleteFile` call list. This locks the design in: the cleanup is driven by `TranscriptPaths`, not by a hard-coded suffix list. Adding a field to `TranscriptPaths` will fail this test until the new field is wired into the `EnumerateSiblings` helper — that is the desired pressure.

Pin only the fields each assertion reads. Use `Faker.Create<TaskExecutionContext>()` for unrelated state.

### `ProcessTaskTests` (existing file)

- Delete tests that asserted `IMoveLiveLog.Move` (or rename them).
- Add `CleanPendingArtifactsAfterTheOutcomeHandlerForDoneTasks` — `ICleanPendingArtifacts.Clean` is called once with `TaskOutcome.Done`. Use `MustHaveHappenedOnceExactly().Then(...)` to assert the outcome handler runs first.
- Add `CleanPendingArtifactsAfterTheOutcomeHandlerForBlockedTasks` — same, with `TaskOutcome.Blocked`.
- Add `CleanPendingArtifactsAfterTheOutcomeHandlerForFailedTasks` — same, with `TaskOutcome.Failed`.
- Add `PassTheClassifiedOutcomeIntoCleanPendingArtifacts` — pin the classified outcome explicitly per case and assert `Clean(_, _, expectedOutcome)`.

If task 10 has already shipped, also keep its ordering tests for `runGitWorkflow` (now: outcome handler → clean → git workflow). If task 10 has not shipped, those tests come with task 10.

### Existing tests

- Other `ProcessTaskTests` for history recording, log writing, etc. stay green unchanged.
- Outcome-handler tests stay green — handlers are not modified by this task.

## Implementation Order

**Phase 1 — New cleanup class with outcome routing**
1. Add `CleanPendingArtifactsTests` covering all 11 cases above. Red.
2. Create [CodeWorker/Commands/Run/CleanPendingArtifacts.cs](CodeWorker/Commands/Run/CleanPendingArtifacts.cs) with `ICleanPendingArtifacts` and `CleanPendingArtifacts` per Design. Green.
3. `dotnet test` — confirm green.

**Phase 2 — Wire into ProcessTask**
4. Update `ProcessTaskTests` to expect `ICleanPendingArtifacts.Clean(context, task, outcome)` after the outcome handler, for each outcome. Red.
5. Inject `ICleanPendingArtifacts` into [ProcessTask](CodeWorker/Commands/Run/ProcessTask.cs)'s primary constructor. Replace the existing `moveLiveLog.Move(...)` call with `cleanPendingArtifacts.Clean(context, task, outcome)`. Green.
6. `dotnet test` — confirm green.

**Phase 3 — Retire `MoveLiveLog`**
7. Delete [CodeWorker/Commands/Run/MoveLiveLog.cs](CodeWorker/Commands/Run/MoveLiveLog.cs) and `MoveLiveLogTests.cs`. Confirm no remaining references via global search. Green.

**Phase 4 — Finish**
8. Confirm Autofac resolution: `ICleanPendingArtifacts` has a single implementation, so per [types-and-di.md](.claude/rules/csharp/types-and-di.md) no module entry is required. Verify the dependency graph still builds.
9. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`.
10. Run a real task end-to-end against this repo to confirm `tasks/pending/` is empty after the run, and a forced-failure case puts every artifact in `tasks/failed/` next to the `.md`.

## Constraints

- Do not introduce a setting to control delete-vs-move on Done. Keep the rule fixed: Done deletes, others move. A setting can be added in a follow-up task if needed.
- Do not change [BuildTranscriptPaths.cs](CodeWorker/Claude/BuildTranscriptPaths.cs) or [TranscriptPaths](CodeWorker/Claude/TranscriptPaths.cs).
- Do not change the outcome handlers ([HandleDoneTaskOutcome.cs](CodeWorker/Commands/Run/Outcomes/HandleDoneTaskOutcome.cs), [HandleBlockedTaskOutcome.cs](CodeWorker/Commands/Run/Outcomes/HandleBlockedTaskOutcome.cs), [HandleFailedTaskOutcome.cs](CodeWorker/Commands/Run/Outcomes/HandleFailedTaskOutcome.cs)) beyond what task 10 already requires.
- Do not change [RunGitWorkflow.cs](CodeWorker/Commands/Run/RunGitWorkflow.cs). Where the git workflow is invoked is owned by task 10.
- Do not change [RecoverPendingTasks.cs](CodeWorker/Claude/RecoverPendingTasks.cs).
- Do not introduce a new abstraction over filesystem operations. Use the existing `IFileSystemTools` and `IMoveFile`.
- Follow every rule in `.claude/rules/csharp/` — no exceptions. Primary constructors, block-body methods, file-scoped namespaces, single class per file, inline interface in the same file (single consumer), no `Async` suffix unless overload disambiguation requires it, no `ConfigureAwait(false)`, switch expressions with discard arm.

## Acceptance Criteria

After a Done run:
- `tasks/done/<task>.md` exists.
- `tasks/pending/` no longer contains any sibling for that task — `.prompt.txt`, `.transcript.jsonl`, `.stderr.log`, `.done`, `.wrapper.pid`, `.live.log`, `.wrapper.started`, `.wrapper.log`, `.claude-args.txt` all deleted.
- The per-task `.log` written by [WriteTaskLog.cs](CodeWorker/Commands/Run/WriteTaskLog.cs) is unchanged.

After a Failed run:
- `tasks/failed/<task>.md` exists.
- `tasks/failed/<task>.<sibling>` exists for every sibling that was actually produced (missing siblings are not invented).
- `tasks/pending/` no longer contains any sibling for that task.

After a Blocked run:
- `tasks/blocked/<task>.md` exists.
- `tasks/blocked/<task>.<sibling>` exists for every sibling that was actually produced.
- `tasks/pending/` no longer contains any sibling for that task.

`tasks/run-history.jsonl`, `runs.jsonl`, and `CodeWorker.log` continue to be written exactly as before.

A task whose live log never opened, or whose done sentinel never wrote, finishes without throwing — the cleanup silently skips missing siblings.

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
- [ ] Manually run a successful task and confirm `tasks/pending/` is empty afterwards
- [ ] Manually force a failure (e.g. a deliberately broken task) and confirm every wrapper artifact ends up in `tasks/failed/` next to the `.md`
- [ ] Report results before finishing
