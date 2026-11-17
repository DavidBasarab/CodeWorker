# Phase 5 — Human-Readable Verdict Report

- **Work item:** verdict-model-reporting (see `tasks/todo/foundation/verdict-model-reporting/00-overview.md`)
- **Depends on:** Phase 1
- **Depended on by:** Phase 6
- **Wave:** 2
- **Parallel with:** Phase 4 — both depend only on Phase 1; disjoint write sets; no shared hotspot
- **Risk:** **Low** — pure string formatting; not wired anywhere.

## Touches

- Creates: `CodeWorker.Cli/Commands/Verify/Verdict/FormatVerdictReport.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/Verdict/FormatVerdictReportTests.cs`
- Modifies: nothing
- Exclusive runtime resources: none

Phase 4 runs at the same time in its own worktree. Create or modify **only** these files.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("Usage" — "a human-readable report for the person reviewing the
work the next morning"), `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first — mandatory
(string interpolation only, never `+`).

**Worktree base:** before reading code, run `git merge --ff-only <wave base>` (the commit the
orchestrator names) in your still-clean worktree, then confirm `git rev-parse HEAD` equals it.

Built by Phase 1 (namespace `FatCat.CodeWorker.Cli.Commands.Verify.Verdict`):
`Verdict { VerdictStatus Status; string Message; List<GateVerdict> Gates }`,
`GateVerdict { string Name; GateStage Stage; GateStatus Status; long DurationMilliseconds; List<GateFinding> Findings }`,
`VerdictStatus { Green, Blocked, InvalidIntent }`; `GateFinding { FilePath; int? Line; Reason }` in `…Gates`.

## Design (build exactly this shape)

`FormatVerdictReport.cs` — interface + class:
```csharp
public interface IFormatVerdictReport
{
	string Format(Verdict verdict);
}
```
Returns the report as lines joined with `Environment.NewLine`:

```
Verdict: BLOCKED — Compile failed: CS1002 ; expected (Foo.cs:12)
  [Failed] Compile (Compile) 120 ms
    Foo.cs:12 — CS1002 ; expected
  [Skipped] Test (Test) 0 ms
```

- Status line: `$"Verdict: {label} — {verdict.Message}"` where label comes from a switch expression:
  Green → `"GREEN"`, Blocked → `"BLOCKED"`, InvalidIntent → `"INVALID INTENT"`, `_` → throw
  `ArgumentOutOfRangeException`.
- One gate line per gate: `$"  [{Status}] {Name} ({Stage}) {DurationMilliseconds} ms"`.
- One finding line per finding, under its gate: `$"    {FilePath}:{Line} — {Reason}"`; without a
  line `$"    {FilePath} — {Reason}"`; without a file `$"    {Reason}"`.
- No gates → the status line only.

## Steps (TDD — tests first, red before green)

`FormatVerdictReportTests` (namespace `Testing.FatCat.CodeWorker.Cli.Commands.Verify.Verdict`), SUT in
the constructor; split the output on `Environment.NewLine` to assert a specific line. One assertion each:

1. `StartWithTheGreenStatusAndMessage`
2. `StartWithTheBlockedStatusAndMessage`
3. `StartWithTheInvalidIntentStatusAndMessage`
4. `WriteOnlyTheStatusLineWhenThereAreNoGates`
5. `WriteALinePerGate` — line count = 1 + gates (no findings).
6. `WriteTheGateStatusNameStageAndDuration` — exact gate line.
7. `WriteAFindingWithFileAndLine`
8. `WriteAFindingWithoutALine`
9. `WriteAFindingWithoutAFile`
10. `PlaceFindingsUnderTheirGate` — order: gate 1, its finding, gate 2.

Red first, then implement to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; switch expression with a throwing discard arm; no banned patterns
- [ ] Only the files in `## Touches` created/modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review` (screen-only — write nothing outside `## Touches`)
- [ ] Exactly one commit on your worktree branch, message referencing this file; no push

Suggested commit message:

```
verdict-model-reporting phase 5: human-readable verdict report (tasks/todo/foundation/verdict-model-reporting/05-verdict-report.md)
```

## Rollback Procedure

- Revert Phase 6 first if committed, then `git revert <this commit>`. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts; deviation log (an empty log is a claim, not a default);
open questions/risks.

## Hand-off

- `IFormatVerdictReport.Format(Verdict) → string` (multi-line). Phase 6 logs it with
  `logger.Information("{Report:l}", report)` so Serilog does not quote it.
- The status line always starts `Verdict: GREEN` / `Verdict: BLOCKED` / `Verdict: INVALID INTENT` —
  E2E tests key on it.
