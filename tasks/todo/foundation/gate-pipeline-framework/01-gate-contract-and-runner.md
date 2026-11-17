# Phase 1 — Gate Contract + Fail-Fast Runner

- **Work item:** gate-pipeline-framework (see
  `tasks/todo/foundation/gate-pipeline-framework/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 2 (`02-gate-timing.md`), Phase 3 (`03-wire-pipeline-into-verify.md`)
- **Wave:** 1
- **Parallel with:** none — it is the contract phase; both other phases build on its types.
- **Risk:** **Low** — new, isolated types and one orchestration class with no IO; nothing is wired
  into `verify` in this phase. No auth/network/data surface.

## Touches

- Creates: `CodeWorker.Cli/Commands/Verify/Gates/` — `GateStage.cs`, `GateStatus.cs`,
  `GateFinding.cs`, `GateResult.cs`, `GatePipelineResult.cs`, `VerificationContext.cs`,
  `IVerificationGate.cs`, `RunGatePipeline.cs`;
  `CodeWorker.Cli.Tests/Commands/Verify/Gates/RunGatePipelineTests.cs`
- Modifies: nothing
- Exclusive runtime resources: none

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("How It Works"), `CLAUDE.md`, and **all**
`.claude/rules/csharp/*.md` first — mandatory. Especially `naming-and-structure.md` (one class per
file; interface in the class's file unless it has many implementations), `types-and-di.md` (plain
classes, collection expressions, primary constructors), `testing.md`, and `toolchain.md` (no
expression-bodied members, including in tests).

Current state you will find:

- `CodeWorker.Cli/Commands/Verify/` holds `VerifyCommand.cs`, `ParseVerifyArguments.cs`,
  `VerifyArgumentsResult.cs`, `VerifyUsageError.cs`, and `Intent/` (`IntentContract`,
  `IntentBehavior`, `IntentResult`, `IntentValidationError`, `ParseIntent`, `ValidateIntent`).
- `Intent/IntentContract.cs`: `public class IntentContract { string Why; string Class; string Responsibility; List<IntentBehavior> Behaviors = []; }`
  (auto-properties), namespace `FatCat.CodeWorker.Cli.Commands.Verify.Intent`.
- **No `Commands/Verify/Gates/` folder exists yet.** Create it.
- DI: `SystemScope` scanning registers every public class against all its interfaces, so
  `IEnumerable<IVerificationGate>` gets every gate automatically (empty when none) and
  `IRunGatePipeline` → `RunGatePipeline` resolves with no module change. **Do not touch
  `CodeWorkerCliModule`.**
- Test stack (`CodeWorker.Cli.Tests/GlobalUsings.cs`): xUnit, FakeItEasy (`A<T>._`, never
  `A<T>.Ignored`), FatCat.Testing (`.Should()`, negation via `.Should().Not…`), FatCat.Fakes
  (`Faker.Create<T>()`). Pattern to copy: `CodeWorker.Cli.Tests/Commands/Verify/VerifyCommandTests.cs`.

## Design (build exactly this shape)

All files in `CodeWorker.Cli/Commands/Verify/Gates/`, namespace
`FatCat.CodeWorker.Cli.Commands.Verify.Gates`. Block bodies everywhere; no records.

`GateStage.cs` — order matters (README order):
```csharp
public enum GateStage { Compile, Test, Mutation, Standards, Intent }
```

`GateStatus.cs`:
```csharp
public enum GateStatus { Passed, Warning, Failed, Skipped }
```

`GateFinding.cs`:
```csharp
public class GateFinding
{
	public string FilePath { get; set; }
	public int? Line { get; set; }
	public string Reason { get; set; }
}
```

`GateResult.cs`:
```csharp
public class GateResult
{
	public string GateName { get; set; }
	public GateStage Stage { get; set; }
	public GateStatus Status { get; set; }
	public List<GateFinding> Findings { get; set; } = [];
	public TimeSpan Duration { get; set; }
}
```
`Duration` is part of the contract now but **stays at its default (zero) in this phase** — Phase 2
fills it. Do not add any timing code here.

`GatePipelineResult.cs`:
```csharp
public class GatePipelineResult
{
	public bool IsBlocked { get; set; }
	public List<GateResult> GateResults { get; set; } = [];
}
```

`VerificationContext.cs` (`using FatCat.CodeWorker.Cli.Commands.Verify.Intent;`):
```csharp
public class VerificationContext
{
	public string IntentPath { get; set; }
	public string ProductionPath { get; set; }
	public string TestsPath { get; set; }
	public IntentContract Intent { get; set; }
}
```

`IVerificationGate.cs` — **standalone interface file** (many implementations will exist):
```csharp
public interface IVerificationGate
{
	string Name { get; }
	GateStage Stage { get; }
	Task<GateResult> Run(VerificationContext context);
}
```

`RunGatePipeline.cs` — interface and class in the same file:
```csharp
public interface IRunGatePipeline
{
	Task<GatePipelineResult> Run(VerificationContext context);
}

public class RunGatePipeline(IEnumerable<IVerificationGate> gates) : IRunGatePipeline
{
	public async Task<GatePipelineResult> Run(VerificationContext context)
	{
		// Behavior (express with small, well-named private methods — no comments in the real code):
		// - order gates by Stage (OrderBy — stable within a stage)
		// - run each in order; take the returned GateResult and stamp GateName = gate.Name, Stage = gate.Stage
		// - after the first result whose Status is Failed, do NOT run the remaining gates; add a
		//   result for each { GateName, Stage, Status = Skipped } (empty findings, zero duration)
		// - Warning, Passed (and anything else) continue
		// - IsBlocked = any result is Failed
	}
}
```
Enumerate `gates` inside `Run`, not in the constructor (the tests add gates to the injected list
after construction). Do not catch exceptions from a gate — a throwing gate is a real fault.

## Steps (TDD — tests first, red before green)

Create `CodeWorker.Cli.Tests/Commands/Verify/Gates/RunGatePipelineTests.cs`, namespace
`Testing.FatCat.CodeWorker.Cli.Commands.Verify.Gates`. Fields: `private readonly List<IVerificationGate> gates = [];`,
`private readonly VerificationContext context = Faker.Create<VerificationContext>();`, and the SUT
`new RunGatePipeline(gates)` built in the constructor. A private helper
`IVerificationGate AddGate(GateStage stage, GateStatus status)` creates `A.Fake<IVerificationGate>()`
with `Name = Faker.Create<string>()`, the given `Stage`, and `Run(A<VerificationContext>._)` returning
`new GateResult { Status = status, Findings = [Faker.Create<GateFinding>()] }`, adds it to `gates`,
and returns it. To assert run order, have the helper record `gate.Name` into a `List<string> runOrder`
via `.Invokes(...)`. One assertion per test, verb-first names:

1. `ReturnNotBlockedWhenThereAreNoGates`
2. `ReturnNoGateResultsWhenThereAreNoGates`
3. `RunEveryGateWhenAllPass` — three passing gates → `GateResults` count is 3.
4. `ReturnNotBlockedWhenAllGatesPass`
5. `PassTheContextToEachGate` — each gate's `Run(context)` must have happened (use one gate per
   assertion if needed — one assertion per test; two tests are fine).
6. `RunGatesInStageOrder` — add Intent, Compile, Mutation → `runOrder` equals Compile, Mutation, Intent names.
7. `ReturnResultsInStageOrder` — same setup → result stages equal `[Compile, Mutation, Intent]`.
8. `KeepInjectedOrderWithinTheSameStage` — two Standards gates → run order matches injection order.
9. `StopRunningGatesAfterTheFirstFailure` — Compile fails, Test passes → Test gate's `Run` `MustNotHaveHappened`.
10. `MarkTheGatesAfterAFailureAsSkipped` — Compile fails, Test, Mutation → statuses `[Failed, Skipped, Skipped]`.
11. `NameTheSkippedGatesOnTheirResults` — skipped result's `GateName` equals the skipped gate's `Name`.
12. `ReturnBlockedWhenAGateFails`
13. `ContinuePastAWarning` — Compile warns, Test passes → Test gate's `Run` `MustHaveHappenedOnceExactly`.
14. `ReturnNotBlockedWhenAGateWarns`
15. `StampTheGateNameOnItsResult`
16. `StampTheGateStageOnItsResult`
17. `KeepTheGateFindingsOnItsResult` — result `Findings` equals the fake's returned findings.

Run them, watch them fail (red), then implement the types and `RunGatePipeline` to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (including the existing E2E suite, unchanged)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; correct type-role suffixes; no banned patterns
      (no records, no expression-bodied members, `[]` collection initializers)
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current task branch, message referencing this file; no push

Suggested commit message:

```
gate-pipeline-framework phase 1: gate contract + fail-fast runner (tasks/todo/foundation/gate-pipeline-framework/01-gate-contract-and-runner.md)
```

## Rollback Procedure

- Revert Phases 2 and 3 first if they are committed (both build on these types), then
  `git revert <this phase's commit>`. No data/config/manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (every departure from
this plan and why — an empty log is a claim, not a default); open questions/risks for the reviewer.

## Hand-off

- **Types (namespace `FatCat.CodeWorker.Cli.Commands.Verify.Gates`):** `IVerificationGate { Name; Stage; Run(VerificationContext) }`,
  `GateStage`, `GateStatus`, `GateFinding`, `GateResult` (with `Duration`, still zero),
  `GatePipelineResult { IsBlocked; GateResults }`, `VerificationContext { IntentPath; ProductionPath; TestsPath; Intent }`,
  `IRunGatePipeline.Run(VerificationContext) → Task<GatePipelineResult>`.
- **For Phase 2:** `RunGatePipeline` has one constructor parameter today (`IEnumerable<IVerificationGate> gates`);
  Phase 2 adds `IClock` and fills `Duration` in the one place results are stamped.
- **For Phase 3:** inject `IRunGatePipeline` into `VerifyCommand`; with zero gates `Run` returns
  `IsBlocked = false` and an empty `GateResults`.
