# Engine — Epic Plan

- **Epic (group):** `### Engine (in-process, C#) — the sub-10s foundation` in `epics.md` (MVP)
- **Source of truth:** `CodeWorker.Cli/README.md` ("The engine runs in-process — that's how it stays
  under 10 seconds")
- **Generated:** 2026-11-20-130226
- **Workflow:** plan one item wave → build it → adjust → plan the next. Items below that are not yet
  planned stay as one-liners on purpose (see "Ordering rule"). Nothing here writes production
  code — expanding an item (Mode B) writes its `task.md` + phased plan; building it is a separate,
  human-driven run.

## What Engine is

The two items below are the **in-process C# engine** the deterministic gates sit on. The README's
speed promise depends on them. There is no per-call `dotnet build`, `dotnet test` or Stryker. The
changed class and its test compile in memory with Roslyn against assemblies that are already built,
and the covering tests run inside the `verify` process.

Engine builds **capabilities, not gates**. It gives the C# engine real compile and test operations
behind Foundation's `IResolveLanguageEngine` seam. It does **not** add an `IVerificationGate` and does
**not** touch `VerifyCommand`. The Compile, Test and Mutation gates (the next group, *Deterministic
gates*) are thin layers that call these capabilities and turn their results into `GateResult`s.
When Engine is done, the engine can compile a unit of work to an in-memory assembly and run its
tests in a collectible load context. Nothing in `verify` calls it yet.

## Current state that shapes the design (verified against the code)

- **The seam exists but carries identity only.** `Commands/Verify/Engines/ILanguageEngine.cs` exposes
  only `Language` and `FileExtensions`. `CSharpLanguageEngine` returns `"C#"` / `[".cs"]`.
  `ResolveLanguageEngine.Resolve(productionPath)` picks the engine by file extension and returns a
  `LanguageEngineResult` value (`None` / `NoEngineForExtension` / `MultipleEnginesForExtension`). The
  seam's ADR-1 (`tasks/done/foundation/language-engine-seam/00-overview.md`) deliberately left out
  `Compile`/`Test` members "until the `Engine` group has a real implementation to shape them." This
  group adds those members.
- **Gates get a `VerificationContext`** (`Gates/VerificationContext.cs`) with `IntentPath`,
  `ProductionPath`, `TestsPath` and the parsed `IntentContract`. These paths are what the engine
  receives. The pipeline runner and the verdict exist and need no change here.
- **No Roslyn yet.** No project references `Microsoft.CodeAnalysis.*`. `CodeWorker.Cli.csproj`
  references only CSharpier.MsBuild, FatCat.Toolkit and Serilog. Item 1 adds the Roslyn package.
- **What a typical test project looks like here** (`CodeWorker.Cli.Tests`): `net11.0`,
  `ImplicitUsings` enabled, a `GlobalUsings.cs` (xUnit, FakeItEasy, FatCat.Testing, FatCat.Fakes),
  **xUnit 2.9.3**, and a `ProjectReference` to the production project. Its build output already holds
  every dependency DLL, the production DLL included. **`bin/Debug/` holds both `net10.0/` (stale) and
  `net11.0/`**, so locating "the existing bin output" must follow the project's `TargetFramework`,
  not just take any folder.
- **Every `verify` call is a new process.** There is no resident server, so Roslyn is cold
  (JIT included) on every call. See Assumptions.

## Items (dependency order)

### 1. In-process compilation — `in-process-compilation`
- **Scope:** Compile the unit of work in memory with Roslyn and return structured diagnostics plus,
  on success, the emitted assembly. The unit of work is the production file, the test file, and the
  test project's global usings. It resolves its reference set from the test project's **existing
  build output** for its `TargetFramework`. It runs no MSBuild and no `dotnet build`. Being able to
  compile is **not** pass/fail. A compile with errors is a successful *run* that returns error
  diagnostics; the Compile gate (next group) decides that this blocks. Only an inability to compile
  at all is an engine failure, returned as a value. Examples are no owning `.csproj` found and
  missing or never-built output. This item gives the C# engine its first real member behind the seam.
- **Depends on:** — (Foundation's `IResolveLanguageEngine` seam is built)
- **Depended on by:** 2. Also the Compile gate and Mutation gate in *Deterministic gates*.
- **Acceptance shape:**
  - A clean unit of work compiles to an in-memory assembly with no error diagnostics.
  - A unit with a deliberate error returns that error's id, message, file and line/column.
  - A test file using the test project's global usings (`Xunit`, `FakeItEasy`, …) compiles without
    restating them.
  - An unbuilt project, or one with no owning `.csproj`, returns a distinct engine-error value.
  - Picking the reference set and the output folder is unit-tested with a faked file system.
  - A timed smoke run on a real unit from this repo is recorded against the sub-10s budget.
- **Plan:** not yet planned  <!-- becomes: tasks/todo/engine/in-process-compilation/ -->

### 2. In-process test runner — `in-process-test-runner`
- **Scope:** Load the compiled test assembly from item 1 into a **collectible
  `AssemblyLoadContext`**. Its dependencies resolve from the test project's build output. Run the
  covering tests in that process and return **pass/fail per test** with failure messages, in
  milliseconds. Then unload the context, so the Mutation gate can repeat the cycle per mutant without
  leaking assemblies. **Covering tests** here means the tests in the submitted test file. Per-line
  coverage scoping belongs to *Scope-to-diff* and the Mutation gate. A failing test is a successful
  *run* with a failed result; the Test gate decides that this blocks. A load failure, such as a missing
  dependency, is a returned engine-error value.
- **Depends on:** 1 (it runs what item 1 emits, against item 1's reference resolution)
- **Depended on by:** — inside Engine. Also the Test gate and Mutation gate in *Deterministic gates*.
- **Acceptance shape:**
  - A unit whose tests all pass returns every test as passed.
  - A unit with a failing assertion returns that test as failed, with its message.
  - Tests in the submitted file are the only ones run, never the rest of the stale test DLL.
  - The load context unloads after the run, proven by a weak-reference check or repeated runs.
  - A timed smoke run on a real unit is recorded against the budget.
- **Plan:** not yet planned  <!-- becomes: tasks/todo/engine/in-process-test-runner/ -->

## Group decisions (lightweight ADRs)

### ADR-G1 — Real engine members go on `ILanguageEngine`; Roslyn code lives in `Engines/CSharp/`
**Decision:** The engine operations become members of `ILanguageEngine`, so gates reach them only
through `IResolveLanguageEngine`. Item 1 adds the compile operation; item 2 adds the test-run
operation. The members' result types are **language-neutral**: a compilation result with
diagnostics (file, line, column, id, message, severity) and the emitted assembly; a test-run result
with per-test outcomes. These types live in `Commands/Verify/Engines/`. Everything Roslyn- or
`AssemblyLoadContext`-specific lives in `Commands/Verify/Engines/CSharp/`
(`FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp`). `CSharpLanguageEngine` delegates to those
collaborators. The exact member signatures are pinned by each item's Mode B plan.
**Context:** The README says "gates never hard-code a language", and Foundation's seam ADR-1 deferred
these members to this group. Keeping result types neutral means a future second-language engine
(deferred, *Full*) returns the same shapes and no gate changes. ADR-G3 of the Foundation plan places
types in sub-folders under `Commands/Verify/`.
**Alternatives rejected:** separate per-language interfaces resolved outside the seam (gates would
then know about C#); Roslyn types (`Diagnostic`, `CSharpCompilation`) in the public result
(this ties the seam to C#).

### ADR-G2 — The compilation unit is an overlay on the existing build output
**Decision:** The in-memory compilation contains the **submitted production file**, the **submitted
test file**, and the **test project's global usings**. Those are its `GlobalUsings.cs` plus the
SDK-generated implicit-usings file under `obj/`, or an equivalent set derived from `ImplicitUsings`.
It references **every assembly in the test project's build output** for its `TargetFramework`, plus
the framework references. That output already contains the production DLL and all packages. The
submitted source types **shadow** their stale copies in the referenced DLLs: CS0436 is expected and
suppressed for this compilation only. No other project source is recompiled.
**Context:** `epics.md` says to compile "the changed class + its test in-memory … resolving
reference assemblies once from the project's existing `bin` output (no MSBuild per call)". The README
says "against the project's already-built reference assemblies. No `dotnet build`, no MSBuild per
call." Compiling two or three files is what keeps a cold-Roslyn call inside the budget.
**Alternatives rejected:** recompiling the whole production and test projects in memory (correct
even when other classes changed, but several times the source volume, plus reading
`project.assets.json` to rebuild the package graph); shelling out to `dotnet build` (the overhead
the README rules out).
**Consequence:** Changes to *other* classes since the last build are not seen. The unit of work is one
class (README), and the Hook wiring epic verifies each changed class in turn. This is accepted,
not hidden (see Assumptions).

### ADR-G3 — Engine outcomes are values; a red compile or test is data, not an engine error
**Decision:** Two kinds of outcome are kept apart. **Engine errors** mean the engine could not do its
job: no owning `.csproj`, build output missing for the `TargetFramework`, an assembly that will not
load. They are returned as an enum-backed result value, following Foundation's ADR-G2 and the
`LanguageEngineResult` pattern already in `Engines/`. **Domain results** are compile errors and
failing tests. They are a *successful* engine run carrying red data, and only the gates (next group)
turn them into a blocking `GateResult`. Exceptions stay reserved for genuinely unexpected faults.
**Context:** `errors-and-logging.md` says to return an enum for known failures. This also keeps the
Engine group free of gate policy: the configuration epic decides block vs warn, and the engine
decides neither.
**Alternatives rejected:** treating a compile error as an engine failure (merges "the code is wrong"
with "the tool is broken"; the verdict and exit codes need them apart).

### ADR-G4 — Thin low-level wrappers are excluded from coverage; the logic around them is unit-tested
**Decision:** Only code that talks straight to the OS or runtime gets `[ExcludeFromCodeCoverage]`
with a specific `Justification`, behind a narrow capability interface. That means
`AssemblyLoadContext` load/unload and raw file reads that FatCat's `IFileSystemTools` does not
cover. Everything with branching is unit-tested with fakes per `testing.md`: finding the owning
`.csproj`, choosing the output folder, building the reference set and source set, mapping
diagnostics, and selecting tests. Roslyn compilation runs fully in memory on source strings, so it is
unit-testable as-is. Prefer testing it with real tiny sources over excluding it.
**Context:** `testing.md`, "Low-Level API Implementations — No Unit Tests Required", says it must not
be applied to classes with branching logic.
**Alternatives rejected:** excluding the whole engine as "infrastructure" (hides the discovery logic
that is most likely to be wrong).

### ADR-G5 — Engine does not touch the pipeline, the verdict or `VerifyCommand`
**Decision:** Engine items create files only under `Commands/Verify/Engines/` (and the mirrored test
folder), plus the `ILanguageEngine` member additions and the csproj package reference. Wiring into
`verify` is the *Deterministic gates* group's job.
**Context:** `VerifyCommand.cs` was the shared hotspot in Foundation's last wave. Keeping Engine off
it lets Engine and gate planning proceed without collisions, and it keeps each Engine item
revertible on its own.
**Alternatives rejected:** a temporary `verify` call into the engine "to see it work" (a throwaway
caller that the Compile gate would immediately replace; the timed smoke run covers the need).

## Item waves

| Wave | Items | State | Why independent |
|---|---|---|---|
| 1 | 1 `in-process-compilation` | Not planned | — |
| 2 | 2 `in-process-test-runner` | Not planned | — (single item) |

**Critical path:** 1 → 2. This is a chain by necessity. The test runner loads the assembly that
compilation emits and reuses its reference resolution (the output folder that dependencies load
from), so it cannot be built or meaningfully tested before item 1's contract exists. Parallelism in
this group comes **inside** each item: Mode B splits each into a contract phase followed by fanned-out
phases with disjoint files. Example for item 1: project and output discovery in parallel with the
Roslyn compile wrapper. A useful cross-group overlap: when item 1 lands, the **Compile gate**
(*Deterministic gates*) depends only on item 1 and can be planned and built alongside item 2.

## Ordering rule

Detailed phased plans are written **one item wave at a time** (Mode B), only when the wave is next.
Items in the same wave are planned together and built in parallel on separate branches. Reverting
or re-planning a built item never strands a downstream plan, because downstream items are not
planned until reached. If building item 1 changes the compile contract, only item 1's plan exists
to reconcile. Item 2 is still a one-liner here and can be edited freely.

## Next item

**`in-process-compilation`** — say **"plan in-process compilation"** (or "plan the next Engine
wave") to expand it into `tasks/todo/engine/in-process-compilation/`.

## Assumptions & open questions

- **[Design-changing — decide in item 1's plan] Cold Roslyn per call.** `verify` is a fresh process
  each time, so the first `CSharpCompilation.Emit` pays Roslyn's JIT and metadata load, typically
  1–3 s, before any test runs. Item 1's timed smoke run measures it. If it threatens the sub-10s
  budget, the mitigations are ReadyToRun publish of the CLI (belongs to *Publish + install*) or a
  warm resident process (a new epic). Neither is designed here.
- **[Decide in item 1's plan] Which build output.** The test project's `TargetFramework` from its
  `.csproj` picks `bin/<Configuration>/<tfm>/` (a stale `net10.0/` sits beside `net11.0/` today).
  Configuration is still open: default `Debug`, the most recently written, or configured later by
  *Tool-level configuration*.
- **[Decide in item 1's plan] Framework references.** Compile against the reference pack
  (`dotnet/packs/Microsoft.NETCore.App.Ref/<version>/ref/<tfm>`) or the CLI's own trusted platform
  assemblies. Both are `net11.0` today; the ref pack is the correct one if they ever diverge.
- **[Decide in item 1's plan] Implicit usings.** Include the SDK-generated
  `obj/<Configuration>/<tfm>/<Project>.GlobalUsings.g.cs` (exists only after a build, which the
  overlay requires anyway), or synthesize the `ImplicitUsings` set.
- **[Design-changing — ask when planning item 2] How tests run.** Either use xUnit's own in-process
  runner (`xunit.runner.utility` for v2.9.3: full xUnit semantics including `[Theory]`, fixtures and
  `IAsyncLifetime`) or a minimal reflection runner for `[Fact]`. The house style uses `[Fact]` and
  constructor setup, so the minimal runner is smaller and faster but diverges if a theory or fixture
  appears. The repo is on xUnit v2; v3's out-of-process model would change this answer.
- **Stale classes other than the one under test.** This follows from ADR-G2. A unit of work whose
  test depends on *another* class changed since the last build sees that class's built version.
  This is accepted for MVP: one class per unit, and the Stop hook verifies each changed class.
- **Strong naming / `InternalsVisibleTo`.** The CLI assembly is signed; the overlay assembly is
  emitted unsigned with a unique name per compile, which the Mutation gate needs for repeated
  emits. If a production project grants `InternalsVisibleTo` to its test assembly, the overlay's
  name will not match. The house rule is public-by-default, so this is assumed rare. Item 1 confirms
  the risk and does not solve it.
