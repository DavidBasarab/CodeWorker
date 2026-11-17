# gate-pipeline-framework — Overview

- **Work item:** `gate-pipeline-framework` (Foundation item 3 — see
  [`tasks/todo/foundation/00-epic-plan.md`](../../../todo/foundation/00-epic-plan.md))
- **Source of truth:** `CodeWorker.Cli/README.md` ("How It Works")
- **Generated:** 2026-11-17-131956

## Work Item

Add the `IVerificationGate` abstraction and the fail-fast runner (`IRunGatePipeline` /
`RunGatePipeline`) under `Commands/Verify/Gates/`. The runner orders gates by `GateStage`, runs each
against one shared `VerificationContext`, short-circuits on the first `Failed`, marks the rest
`Skipped`, and stamps each result with the gate's identity and duration. It ships with **zero
concrete gates**. `verify` then runs it after a valid intent and logs a summary. See `task.md` for
the full specification and acceptance criteria.

### Current state that shapes the design (verified against the code, 2026-11-17)

- **Items 1–2 are built and merged** (PR #12, PR #13). `CodeWorker.Cli/Commands/Verify/`:
  - `VerifyCommand.cs` — `VerifyCommand(IParseVerifyArguments, IParseIntent, ILogger)`; `Execute` is
    `async`: parse args → on invalid, `logger.Error("verify: {Reason}", …)` and return → log parsed
    paths → `await parseIntent.Parse(arguments)` → on invalid, log error and return → private
    `LogIntentSummary` logs `"verify: intent for {Class} declares {BehaviorCount} behaviors"`. That is
    the point where the pipeline runs (intent phase 3 hand-off).
  - `VerifyArgumentsResult { IsValid, Error, Message, IntentPath, ProductionPath, TestsPath }`.
  - `Intent/IntentContract { Why, Class, Responsibility, List<IntentBehavior> Behaviors }`,
    `Intent/IntentResult { IsValid, Error, Message, Intent }`.
- **No `Commands/Verify/Gates/` folder exists.** ADR-G3 places this item's types there, namespace
  `FatCat.CodeWorker.Cli.Commands.Verify.Gates`.
- **DI scanning registers *every* implementation, not just single ones.** `SystemScope.BuildContainer`
  (`C:\Code\FatCat.Toolkit\src\ToolKit\Injection\SystemScope.cs`) calls
  `RegisterAssemblyTypes(...).AsImplementedInterfaces().HasPublicConstructor().PublicOnly().PreserveExistingDefaults().AsSelf()`.
  So `IEnumerable<IVerificationGate>` automatically contains every public gate class, in
  **assembly-scan order** (not a contract), and Autofac resolves it as **empty** when there are none.
  Consequences: no `CodeWorkerCliModule` change is needed (this corrects epic-plan ADR-G5), and the
  runner must order gates explicitly (ADR-2).
- **No clock abstraction in the CLI.** The main project has `CodeWorker/Claude/IClock.cs`
  (`IClock { DateTime UtcNow { get; } }` + `SystemClock`). The CLI does not reference the main
  project. `types-and-di.md` requires `IClock` where time drives testable logic — per-gate duration does.
- **Test stack** (`CodeWorker.Cli.Tests/GlobalUsings.cs`): xUnit, FakeItEasy, FatCat.Testing,
  FatCat.Fakes. `VerifyCommandTests` fakes collaborators with `current*` fields + `ReturnsLazily`.
- **E2E** (`CodeWorker.Cli.EndToEnd.Tests`, part of `dotnet test CodeWorker.slnx`) publishes the CLI
  and drives it as a process. `Harness/TempWorkspace` writes a **valid** intent for `Foo` by default
  and has `WriteIntent(string)` for invalid cases. Serilog's console sink writes to stdout; strings
  render quoted, numbers and booleans unquoted (`False`).

## Acceptance Criteria → Phase Map

| Acceptance criterion (task.md) | Proven by |
|---|---|
| `IVerificationGate` contract (`Name`, `Stage`, `Run`) | Phase 1 |
| Runs gates in `GateStage` order; stable within a stage | Phase 1 |
| Every gate receives the same context | Phase 1 |
| First `Failed` stops the pipeline; later gates `Skipped`; blocked | Phase 1 |
| `Warning` neither stops nor blocks | Phase 1 |
| All pass → all results, not blocked; zero gates → empty, not blocked | Phase 1 |
| Result carries name, stage, status, findings | Phase 1 |
| Result carries duration via `IClock`; skipped = zero | Phase 2 |
| `verify` runs the pipeline only after valid args + intent, with the right context; logs the summary | Phase 3 (unit) |
| E2E: `finished with 0 gates`; invalid intent never runs the pipeline; exit 0 | Phase 3 (E2E) |
| Zero warnings, all green | Every phase + the integrated build after each wave |

## Phases & Dependency Graph

| Phase | File | Risk | Depends on | Depended on by |
|---|---|---|---|---|
| 1 — Gate contract + fail-fast runner (`IVerificationGate`, `GateStage`, `GateStatus`, `GateFinding`, `GateResult`, `GatePipelineResult`, `VerificationContext`, `IRunGatePipeline`/`RunGatePipeline`) | `01-gate-contract-and-runner.md` | **Low** — new, isolated types and a pure orchestration class; not wired anywhere | — | 2, 3 |
| 2 — Clock abstraction + per-gate timing (`Time/SystemClock.cs`, `IClock` into `RunGatePipeline`) | `02-gate-timing.md` | **Low** — one new wrapper over `DateTime.UtcNow`; one new constructor dependency on the runner | 1 | — |
| 3 — Run the pipeline from `verify` + E2E | `03-wire-pipeline-into-verify.md` | **Medium** — changes the runtime path of every `verify` call; the summary line is what the E2E suite and later items key on | 1 | — |

**Revert cascade (reverse dependency order):** phases 2 and 3 are independent of each other —
revert either alone. Reverting phase 1 requires reverting 2 and 3 first.

### Execution waves (parallel subagents)

| Wave | Phases | Runs as | Why parallel-safe |
|---|---|---|---|
| 1 | 1 | Single subagent on the task branch | — (contract phase) |
| 2 | 2, 3 | **Two parallel subagents**, each in its own worktree; commits cherry-picked in phase order | Neither depends on the other; disjoint write sets (below); no shared hotspot (no `*Module.cs`, `GlobalUsings.cs`, `*.csproj`, README, or E2E `Harness/` edit) |

**Critical path:** 1 → 3 (two sequential steps).

Wave 2 file sets (disjoint — proven):

| Phase | Creates | Modifies |
|---|---|---|
| 2 | `CodeWorker.Cli/Time/SystemClock.cs` | `CodeWorker.Cli/Commands/Verify/Gates/RunGatePipeline.cs`, `CodeWorker.Cli.Tests/Commands/Verify/Gates/RunGatePipelineTests.cs` |
| 3 | `CodeWorker.Cli.EndToEnd.Tests/VerifyGatePipelineTests.cs` | `CodeWorker.Cli/Commands/Verify/VerifyCommand.cs`, `CodeWorker.Cli.Tests/Commands/Verify/VerifyCommandTests.cs` |

**Why this split maximizes parallelism.** The critical path is two waves no matter how the work is
cut: wiring (phase 3) needs the runner's interface and a real implementation for the E2E publish,
so it cannot start before phase 1. Timing was pulled **out** of phase 1 so wave 1 is as short as
possible and the timing work runs alongside the wiring instead of in front of it. Phase 1 adds
`GateResult.Duration` (default zero) to the contract so phase 2 only fills it and never touches a
file phase 3 touches. Splitting phase 1 further (a types-only "contract" phase) was rejected: it
would add a wave, not remove one, because phase 3's E2E still needs the real runner.

**Integration check after wave 2.** Phase 3 is proven against phase 1's runner (no clock). After
both cherry-picks, `RunGatePipeline` needs `IClock` — resolved by scanning from phase 2's
`SystemClock` — so the orchestrator's integrated `dotnet build` + `dotnet test` (including E2E) is
what proves the combination.

## Orchestrator

`orchestrator.md` (this folder) is the runbook. Say **"run gate-pipeline-framework"** — it runs
wave 1 (phase 1), then wave 2 (phases 2 and 3 as parallel worktree subagents), cherry-picks in phase
order, verifies the integrated build and tests, and halts on failure. It never squashes, amends,
rebases, or pushes.

## Decisions (lightweight ADRs)

### ADR-1 — The gate contract: `Name`, `Stage`, `Run(VerificationContext)`
**Decision:**
```csharp
public interface IVerificationGate
{
	string Name { get; }
	GateStage Stage { get; }
	Task<GateResult> Run(VerificationContext context);
}
```
In its **own file** `Gates/IVerificationGate.cs` — it will have many implementations
(`naming-and-structure.md` allows a standalone interface file exactly then). `Run` is async because
the real gates do IO (compile, test, LLM).
**Context:** README "How It Works"; `epics.md` item text. `Name` distinguishes gates that share a
stage (the Full milestone has two Standards gates).
**Alternatives rejected:** a capability-style name like `IVerifyCode` (the epic and epic plan name
it `IVerificationGate`; it names a role with many implementations, like `ICommand`); a synchronous
`Run` (every concrete gate is IO-bound — async all the way).

### ADR-2 — Order by an explicit `GateStage` enum, not by registration
**Decision:** `GateStage { Compile, Test, Mutation, Standards, Intent }` in README order. The runner
runs `gates.OrderBy(gate => gate.Stage)` — `OrderBy` is stable, so gates sharing a stage keep
their injected order.
**Context:** `SystemScope` scanning injects gates in assembly-scan order (verified), which is not a
contract. The README fixes the order ("cheapest and most objective first"), so the order belongs in
code, visible and testable.
**Alternatives rejected:** an `int Order` property (magic numbers spread across gate classes);
explicit ordered registration in `CodeWorkerCliModule` (fights scanning, untestable, and scanning
would still register duplicates); ordering inside each gate's constructor (no).

### ADR-3 — `GateStatus { Passed, Warning, Failed, Skipped }`; only `Failed` blocks
**Decision:** A gate returns `Passed`, `Warning`, or `Failed`. `Failed` stops the pipeline and
blocks it; `Warning` is recorded and the pipeline continues. `Skipped` is written by the runner for
every gate after the first `Failed` (never run, zero duration, no findings).
`GatePipelineResult.IsBlocked` is true exactly when some result is `Failed`.
**Context:** `epics.md`: "short-circuit on the first *blocking* failure"; README config decides
"which gates block vs. warn". Listing the skipped gates means item 4's verdict and morning report can
say "Mutation: skipped (Compile failed)" without re-deriving the gate list.
**Alternatives rejected:** omitting skipped gates from the result (the verdict loses the full
picture); a per-result `IsBlocking` flag (two fields encode one fact); reading block/warn config now
(Integration group — until then a gate chooses `Failed` vs `Warning` itself).

### ADR-4 — Gates return status + findings; the runner stamps identity and duration
**Decision:** `GateResult { string GateName; GateStage Stage; GateStatus Status; List<GateFinding> Findings = []; TimeSpan Duration }`.
A gate returns a `GateResult` with `Status` and `Findings`; the runner sets `GateName`, `Stage`
(from the gate), and `Duration` (phase 2). `GateFinding { string FilePath; int? Line; string Reason }` —
`Line` is a genuinely optional value type (a finding about a whole file has no line).
`GatePipelineResult { bool IsBlocked; List<GateResult> GateResults = [] }`, in run order.
**Context:** `epics.md`: "per-gate result (pass/fail, locations, reasons, timing)". Stamping in one
place stops every gate from repeating its own identity and timing code. Plain classes,
auto-properties, collection expressions (ADR-G4).
**Alternatives rejected:** a separate `GateOutcome` type for what gates return (two near-identical
types); a `Column` on findings (the Compile gate item can add it when it needs it); records (banned).

### ADR-5 — Duration through an injected `IClock`; the CLI gets its own copy
**Decision:** Add `CodeWorker.Cli/Time/SystemClock.cs` holding `IClock { DateTime UtcNow { get; } }`
and `SystemClock` (namespace `FatCat.CodeWorker.Cli.Time`), mirroring the main project's
`CodeWorker/Claude/IClock.cs`. `SystemClock` is `[ExcludeFromCodeCoverage]` with a specific
justification (direct `DateTime.UtcNow` wrapper). The runner reads `clock.UtcNow` before and after
`await gate.Run(context)`; `Duration = after - before`.
**Context:** `types-and-di.md` and `not-allowed.md`: inject `IClock` where time drives testable
logic. The CLI must not reference the overnight-runner project.
**Alternatives rejected:** `Stopwatch` (not fakeable — durations untestable); referencing the
`CodeWorker` project for `IClock` (couples the CLI to the task runner); moving `IClock` into
FatCat.Toolkit now (a toolkit change is a separate task — flagged in Open Questions).

### ADR-6 — `VerificationContext` carries paths + intent and is the gates' shared working state
**Decision:** `VerificationContext { string IntentPath; string ProductionPath; string TestsPath; IntentContract Intent }`
in `Gates/`. `VerifyCommand` builds it from the valid `VerifyArgumentsResult` and
`IntentResult.Intent`. It is a mutable `*Context` (the house suffix for working state through one
unit of work), so Engine-group gates can later add shared state (e.g. the in-memory compilation the
Test and Mutation gates reuse).
**Context:** The epic plan's item 3 scope: "the runner consumes the intent context (item 2) and the
parsed arguments (item 1)". Flattening the paths keeps `IsValid`/`Error`/`Message` (meaningless once
valid) out of what gates see.
**Alternatives rejected:** passing `VerifyArgumentsResult` + `IntentContract` as two parameters
(every future shared value becomes another parameter on every gate); embedding the whole
`VerifyArgumentsResult` (leaks validation fields into gates).

### ADR-7 — No module registration (corrects epic-plan ADR-G5)
**Decision:** `CodeWorkerCliModule` is not changed. `RunGatePipeline(IEnumerable<IVerificationGate> gates)`
receives every gate through scanning; zero gates resolve as an empty sequence. `IClock` →
`SystemClock` and `IRunGatePipeline` → `RunGatePipeline` are single implementations, also scanned.
**Context:** Verified `SystemScope.BuildContainer` (see Current state). ADR-G5 assumed multi-impl
seams needed module registrations; scanning already does it. The epic plan is updated to say so.
**Alternatives rejected:** registering gates explicitly (duplicates scanning; order would still be
scan-dependent for single resolves due to `PreserveExistingDefaults`).

### ADR-8 — `verify` keeps the intent summary and adds a pipeline summary; exit code untouched
**Decision:** After `LogIntentSummary`, `VerifyCommand` builds the context, awaits
`runGatePipeline.Run(context)`, and logs
`"verify: gate pipeline finished with {GateCount} gates, blocked: {IsBlocked}"` with
`GateResults.Count` and `IsBlocked`. Not run when args or intent are invalid. Exit code stays 0.
**Context:** The intent-model phase-3 hand-off pointed here. The existing E2E (`VerifyIntentTests`)
asserts the intent summary, so it stays. The summary line gives the E2E suite something deterministic
to assert with zero gates. Item 4 replaces logging with the verdict + exit code.
**Alternatives rejected:** replacing the intent summary (breaks existing E2E for no gain); setting
`Environment.ExitCode` now (item 4's contract).

## Assumptions

- **Nullable is disabled** — plain reference types; `int? Line` is the one deliberate `?` (value type).
- **`ImplicitUsings` enabled** — `System.Linq`, `System.Collections.Generic` implicit;
  `System.Diagnostics.CodeAnalysis` needs an explicit `using` for `[ExcludeFromCodeCoverage]`.
- **Autofac resolves an unregistered `IEnumerable<T>` as empty** (standard implicit relationship type).
- **FakeItEasy `ReturnsNextFromSequence`** drives the fake clock in phase 2.
- **Task branch:** the human creates `gate-pipeline-framework` from `main` (per the `New-Branch`
  workflow) before running; the orchestrator works on the current branch and stops if it is `main`.

## Open Questions

None blocking. Flag for the human reviewer:

- **`IClock` duplicated in two projects.** Moving `IClock`/`SystemClock` into FatCat.Toolkit would
  remove the copy — a toolkit task (`C:\Code\FatCat.Toolkit\src\task\todo`) if wanted.
- **`GateStage` names gates not built yet.** The enum lists the README's five stages up front so the
  order is fixed in one place; members gain implementations as the gate items land.
- **`.claude/rules/csharp/types-and-di.md` is imprecise.** It says scanning "auto-registers every
  interface that has a single implementation"; `SystemScope` actually registers all implementations
  (`PreserveExistingDefaults` makes the first one win for a single resolve). Worth correcting in the
  rule file.
- **Block-vs-warn policy** stays inside each gate until the Integration group's tool-level config
  exists; the runner will then map a gate's `Failed` to `Warning` by config.
