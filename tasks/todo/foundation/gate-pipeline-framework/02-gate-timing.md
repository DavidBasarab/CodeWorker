# Phase 2 — Clock Abstraction + Per-Gate Timing

- **Work item:** gate-pipeline-framework (see
  `tasks/todo/foundation/gate-pipeline-framework/00-overview.md`)
- **Depends on:** Phase 1 (`01-gate-contract-and-runner.md`)
- **Depended on by:** —
- **Wave:** 2
- **Parallel with:** Phase 3 — no dependency between them, disjoint write sets, no shared hotspot.
- **Risk:** **Low** — one new wrapper over `DateTime.UtcNow` and one new constructor dependency on
  `RunGatePipeline`. No auth/network/data surface.

## Touches

- Creates: `CodeWorker.Cli/Time/SystemClock.cs`
- Modifies: `CodeWorker.Cli/Commands/Verify/Gates/RunGatePipeline.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/Gates/RunGatePipelineTests.cs`
- Exclusive runtime resources: none

You run in your own worktree while Phase 3 edits `VerifyCommand.cs`, `VerifyCommandTests.cs`, and
adds an E2E test class. Create or modify **only** the files above.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Especially `types-and-di.md` ("IClock — Time Abstraction"), `not-allowed.md` (no
`DateTime.UtcNow` in testable logic), and `testing.md` ("Low-Level API Implementations — No Unit
Tests Required").

Current state you will find (Phase 1 committed):

- `CodeWorker.Cli/Commands/Verify/Gates/RunGatePipeline.cs` — `IRunGatePipeline` +
  `RunGatePipeline(IEnumerable<IVerificationGate> gates)`. It orders gates by `Stage`, runs each,
  stamps `GateName` and `Stage` on the returned `GateResult`, and after the first `Failed` adds
  `Skipped` results without running the rest. `GateResult.Duration` exists but is never set.
- `CodeWorker.Cli.Tests/Commands/Verify/Gates/RunGatePipelineTests.cs` — builds the SUT in the
  constructor from a `List<IVerificationGate> gates` field; an `AddGate(stage, status)` helper adds
  fake gates.
- The CLI has **no clock abstraction**. The main project's version to mirror is
  `CodeWorker/Claude/IClock.cs`:
  ```csharp
  public interface IClock { DateTime UtcNow { get; } }
  public class SystemClock : IClock { public DateTime UtcNow { get { return DateTime.UtcNow; } } }
  ```
  Do **not** reference the `CodeWorker` project from the CLI.
- DI: scanning registers `SystemClock` as `IClock` automatically — no module change.

## Design (build exactly this shape)

**New** `CodeWorker.Cli/Time/SystemClock.cs`, namespace `FatCat.CodeWorker.Cli.Time` — interface and
class in one file (single implementation):

```csharp
using System.Diagnostics.CodeAnalysis;

namespace FatCat.CodeWorker.Cli.Time;

public interface IClock
{
	DateTime UtcNow { get; }
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over DateTime.UtcNow — no business logic, tested via IClock fakes in consuming classes."
)]
public class SystemClock : IClock
{
	public DateTime UtcNow
	{
		get { return DateTime.UtcNow; }
	}
}
```

**Modify** `RunGatePipeline` → `RunGatePipeline(IEnumerable<IVerificationGate> gates, IClock clock)`.
Around each gate that **runs**: read `clock.UtcNow` immediately before `await gate.Run(context)` and
immediately after; set `Duration = after - before` where the result is stamped. A **skipped** gate
reads no clock and keeps `Duration = TimeSpan.Zero`. Keep the change inside the existing stamping
path — do not restructure the ordering / short-circuit logic Phase 1 built.

## Steps (TDD — tests first, red before green)

In `RunGatePipelineTests`: add `private readonly IClock clock = A.Fake<IClock>();` and pass it to the
constructor (`new RunGatePipeline(gates, clock)`). Configure the clock per test with
`A.CallTo(() => clock.UtcNow).ReturnsNextFromSequence(...)` using a base time from
`Faker.Create<DateTime>()` and a random positive offset. **All Phase 1 tests must stay green
unchanged** apart from the constructor call (a default clock returning a fixed time is fine for them).
Add, one assertion each:

1. `StampTheGateDurationOnItsResult` — one passing gate, clock returns `start`, `start + elapsed` →
   result `Duration` equals `elapsed`.
2. `TimeEachGateSeparately` — two gates, clock sequence `t0, t0+a, t1, t1+b` → second result's
   `Duration` equals `b`.
3. `ReportZeroDurationForASkippedGate` — Compile fails, Test skipped → Test result `Duration` is `TimeSpan.Zero`.
4. `NotReadTheClockForASkippedGate` — Compile fails, Test skipped → `clock.UtcNow` read exactly twice.
5. `TimeAFailingGate` — a failing gate still gets its measured `Duration`.

Red first, then implement `SystemClock` and the runner change to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (including the existing E2E suite)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; correct type-role suffixes; no banned patterns
      (no `DateTime.UtcNow` outside `SystemClock`, no `Stopwatch`, no expression-bodied members)
- [ ] Only the files named in Design were created/modified (wave-2 disjointness)
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the isolated worktree branch the orchestrator gave you, message referencing this file; no push

Suggested commit message:

```
gate-pipeline-framework phase 2: clock abstraction + per-gate timing (tasks/todo/foundation/gate-pipeline-framework/02-gate-timing.md)
```

## Rollback Procedure

- `git revert <this phase's commit>` — nothing depends on it. `RunGatePipeline` returns to one
  constructor parameter and `Duration` to zero; Phase 3 is unaffected (it only injects
  `IRunGatePipeline`). No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (every departure from
this plan and why — an empty log is a claim, not a default); open questions/risks for the reviewer.

## Hand-off

- **`IClock` / `SystemClock`** in `FatCat.CodeWorker.Cli.Time` — the CLI's time abstraction for any
  later class whose logic depends on the current time.
- **`GateResult.Duration`** is now the measured wall-clock time of each gate that ran (zero when
  skipped) — consumed by item 4's verdict and the Full **Per-gate timing in the verdict** item.
