# Phase 1 — Verdict Model + Builder

- **Work item:** verdict-model-reporting (see `tasks/todo/foundation/verdict-model-reporting/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 4, Phase 5, Phase 6
- **Wave:** 1
- **Parallel with:** Phase 2, Phase 3 — no dependency, disjoint write sets, no shared hotspot
- **Risk:** **Low** — new, isolated types and a pure mapping class; nothing is wired.

## Touches

- Creates: `CodeWorker.Cli/Commands/Verify/Verdict/VerdictStatus.cs`, `GateVerdict.cs`, `Verdict.cs`,
  `BuildVerdict.cs`; `CodeWorker.Cli.Tests/Commands/Verify/Verdict/BuildVerdictTests.cs`
- Modifies: nothing
- Exclusive runtime resources: none

Other phases run at the same time in their own worktrees. Create or modify **only** these files.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("Usage"), `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md`
first — mandatory (plain classes, no records, `[]` initializers, block bodies, one class per file,
switch expressions with a throwing discard arm).

**Worktree base:** before reading code, run `git merge --ff-only <wave base>` (the commit the
orchestrator names) in your still-clean worktree, then confirm `git rev-parse HEAD` equals it — the
worktree tool starts from `main`.

Existing types you build on (namespace `FatCat.CodeWorker.Cli.Commands.Verify.Gates`):
- `GatePipelineResult { bool IsBlocked; List<GateResult> GateResults }` — results in run order.
- `GateResult { string GateName; GateStage Stage; GateStatus Status; List<GateFinding> Findings; TimeSpan Duration }`.
- `GateFinding { string FilePath; int? Line; string Reason }`.
- `GateStatus { Passed, Warning, Failed, Skipped }`; `GateStage { Compile, Test, Mutation, Standards, Intent }`.

And `FatCat.CodeWorker.Cli.Commands.Verify.Intent.IntentResult { bool IsValid; IntentValidationError Error; string Message; IntentContract Intent }`.

## Design (build exactly this shape)

Folder `CodeWorker.Cli/Commands/Verify/Verdict/`, namespace `FatCat.CodeWorker.Cli.Commands.Verify.Verdict`.

```csharp
public enum VerdictStatus { Green, Blocked, InvalidIntent }

public class GateVerdict
{
	public string Name { get; set; }
	public GateStage Stage { get; set; }
	public GateStatus Status { get; set; }
	public long DurationMilliseconds { get; set; }
	public List<GateFinding> Findings { get; set; } = [];
}

public class Verdict
{
	public VerdictStatus Status { get; set; }
	public string Message { get; set; }
	public List<GateVerdict> Gates { get; set; } = [];
}
```

`BuildVerdict.cs` — interface + class in one file:
```csharp
public interface IBuildVerdict
{
	Verdict Build(GatePipelineResult pipelineResult);
	Verdict BuildInvalidIntent(IntentResult intentResult);
}
```
- `Build`: `Status = pipelineResult.IsBlocked ? Blocked : Green`; `Gates` = one `GateVerdict` per
  `GateResult`, same order (`DurationMilliseconds = (long)result.Duration.TotalMilliseconds`,
  `Findings` = the result's findings). Message per ADR-2:
  - Green → `"No gate failed."`
  - Blocked → first result with `Status == Failed`; with its first finding:
    `"{GateName} failed: {Reason} ({FilePath}:{Line})"`; omit `:{Line}` when `Line` is null; omit the
    whole ` ({…})` when `FilePath` is null; with no findings → `"{GateName} failed."`
- `BuildInvalidIntent`: `Status = InvalidIntent`, `Message = intentResult.Message`, `Gates = []`.

Keep methods small and named (e.g. `BlockedMessage`, `Location`, `ToGateVerdict`); no comments.

## Steps (TDD — tests first, red before green)

`CodeWorker.Cli.Tests/Commands/Verify/Verdict/BuildVerdictTests.cs`, namespace
`Testing.FatCat.CodeWorker.Cli.Commands.Verify.Verdict`; SUT `new BuildVerdict()` in the constructor.
Build inputs from `Faker.Create<T>()` and pin only the fields each assertion reads. One assertion each:

1. `ReturnGreenWhenThePipelineIsNotBlocked`
2. `ReturnBlockedWhenThePipelineIsBlocked`
3. `SayNoGateFailedWhenGreen`
4. `NameTheFailedGateAndItsFirstFindingWhenBlocked` — full `"Compile failed: reason (Foo.cs:12)"` shape.
5. `UseTheFirstFailedGateWhenSeveralFailed`
6. `OmitTheLineWhenTheFindingHasNone`
7. `OmitTheLocationWhenTheFindingHasNoFile`
8. `SayOnlyTheGateFailedWhenItHasNoFindings`
9. `ListEveryGateInRunOrder` — gate names in order.
10. `CarryTheGateStage`, 11. `CarryTheGateStatus`, 12. `CarryTheGateFindings`
13. `ConvertTheDurationToMilliseconds` — e.g. `TimeSpan.FromMilliseconds(1234)` → `1234`.
14. `ReturnInvalidIntentForAnInvalidIntent`
15. `UseTheIntentErrorMessageForAnInvalidIntent`
16. `ListNoGatesForAnInvalidIntent`

Red first, then implement to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (existing E2E unchanged)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; no banned patterns
- [ ] Only the files in `## Touches` created/modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review` (screen-only — write nothing outside `## Touches`)
- [ ] Exactly one commit on your worktree branch, message referencing this file; no push

Suggested commit message:

```
verdict-model-reporting phase 1: verdict model + builder (tasks/todo/foundation/verdict-model-reporting/01-verdict-model-and-builder.md)
```

## Rollback Procedure

- Revert Phases 6, 4, 5 first if committed, then `git revert <this commit>`. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (an empty log is a claim,
not a default); open questions/risks.

## Hand-off

- `Verdict`, `GateVerdict`, `VerdictStatus`; `IBuildVerdict.Build(GatePipelineResult)` and
  `IBuildVerdict.BuildInvalidIntent(IntentResult)` (namespace `…Commands.Verify.Verdict`).
- Phases 4 and 5 consume `Verdict` only; Phase 6 injects `IBuildVerdict` into `VerifyCommand`.
