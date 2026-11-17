# language-engine-seam — Overview

- **Work item:** `language-engine-seam` (Foundation item 5 — see
  [`tasks/todo/foundation/00-epic-plan.md`](../../../todo/foundation/00-epic-plan.md))
- **Source of truth:** `CodeWorker.Cli/README.md` ("The engine runs in-process", "C#-first,
  language-agnostic by design")
- **Generated:** 2026-11-17-142201
- **Item wave:** 4 — built in parallel with `verdict-model-reporting` (disjoint files; see the epic plan)

## Work Item

Add `ILanguageEngine`, an identity-only `CSharpLanguageEngine`, and `IResolveLanguageEngine`, which
picks the engine for a production file by extension and returns an error value when no engine (or
more than one) matches. See `task.md` for the specification and acceptance criteria.

### Current state that shapes the design (verified against the code, 2026-11-17)

- `CodeWorker.Cli/Commands/Verify/` has `Intent/` and `Gates/`; **no `Engines/` folder** exists.
- `Gates/IVerificationGate.cs` is the precedent for a **standalone interface file** with multiple
  implementations, consumed as `IEnumerable<IVerificationGate>` by `RunGatePipeline`.
- `SystemScope.BuildContainer` registers **every** public implementation of each interface
  (`RegisterAssemblyTypes(...).AsImplementedInterfaces()...PreserveExistingDefaults()`), so
  `IEnumerable<ILanguageEngine>` resolves with every engine and no module entry is needed (epic-plan
  ADR-G5, corrected 2026-11-17). Scan order is not a contract.
- Result-value precedent: `VerifyArgumentsResult { IsValid; Error; Message; … }` + `VerifyUsageError`,
  and `IntentResult { IsValid; Error; Message; Intent }` + `IntentValidationError` — an enum whose
  first member is `None`.
- There is no container/registration test anywhere in `CodeWorker.Cli.Tests`; registration is
  proven by convention and, for gates, by the E2E suite resolving the (empty) gate list.

## Acceptance Criteria → Phase Map

| Acceptance criterion (task.md) | Proven by |
|---|---|
| `ILanguageEngine` declares language + extensions | Phase 1 |
| `CSharpLanguageEngine` reports `C#` / `.cs` | Phase 1 |
| Resolve by extension, case-insensitive | Phase 2 |
| Unsupported / missing extension → `NoEngineForExtension` value + message | Phase 2 |
| Duplicate claim → `MultipleEnginesForExtension` value | Phase 2 |
| Resolver depends only on `IEnumerable<ILanguageEngine>`; fakes in tests | Phase 2 |
| Zero warnings; suite green | Phases 1 and 2 |

## Phases & Dependency Graph

| Phase | File | Risk | Depends on | Depended on by |
|---|---|---|---|---|
| 1 — Engine contract + identity-only C# engine (`ILanguageEngine`, `CSharpLanguageEngine`) | `01-engine-contract-and-csharp-engine.md` | Low — two new types, nothing consumes them | — | 2 |
| 2 — Engine resolver (`IResolveLanguageEngine`/`ResolveLanguageEngine`, `LanguageEngineResult`, `LanguageEngineError`) | `02-engine-resolver.md` | Medium — defines the seam every later gate resolves its engine through | 1 | — |

**Revert cascade:** revert 2, then 1.

### Execution waves (parallel subagents)

| Wave | Phases | Runs as | Why |
|---|---|---|---|
| 1 | 1 | Single subagent | Defines `ILanguageEngine`, which Phase 2 consumes |
| 2 | 2 | Single subagent | Needs the interface from Phase 1 |

**Critical path:** 1 → 2.

This item is a chain on purpose. Splitting the interface into its own "contract" phase would let the
C# engine and the resolver run side by side, but the critical path would still be two steps (contract,
then the pair) and it would add a third subagent and an integration round for two small files. The
parallelism that matters here is at the **item** level: both phases run beside
`verdict-model-reporting`'s waves (see the epic plan's combined schedule), in their own worktrees.

Write sets (disjoint from every `verdict-model-reporting` phase — all under `Engines/`):

| Phase | Creates | Modifies |
|---|---|---|
| 1 | `CodeWorker.Cli/Commands/Verify/Engines/{ILanguageEngine,CSharpLanguageEngine}.cs`, `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharpLanguageEngineTests.cs` | — |
| 2 | `CodeWorker.Cli/Commands/Verify/Engines/{LanguageEngineError,LanguageEngineResult,ResolveLanguageEngine}.cs`, `CodeWorker.Cli.Tests/Commands/Verify/Engines/ResolveLanguageEngineTests.cs` | — |

## Orchestrator

`orchestrator.md` (this folder). Say **"run language-engine-seam"** — or run it together with item 4
(see the epic plan's "Next item"). Both phases run in isolated worktrees and are integrated onto the
`language-engine-seam` branch in its own checkout, so the session's main checkout can stay on
`verdict-model-reporting`. A watchdog runs throughout.

## Decisions (lightweight ADRs)

### ADR-1 — `ILanguageEngine` is a standalone, identity-only interface (user decision 2026-11-17)
**Decision:** `Engines/ILanguageEngine.cs`:
```csharp
public interface ILanguageEngine
{
	string Language { get; }

	IReadOnlyList<string> FileExtensions { get; }
}
```
**Context:** It will have several implementations (C# now, a second language in Full), so it gets
its own file (`naming-and-structure.md`), like `IVerificationGate`. It is named for its role, as
`IVerificationGate` is — the same deliberate exception the `*Command` marker interfaces use. The
user chose identity-only: no speculative `Compile`/`Test` members until the `Engine` group has a
real implementation to shape them.
**Alternatives rejected:** a no-op engine with placeholder compile/test methods (invents a contract
before the Roslyn work defines it); registering nothing concrete (leaves the "C# engine registered"
acceptance unprovable).

### ADR-2 — Resolve by production-file extension, case-insensitive
**Decision:** `IResolveLanguageEngine.Resolve(string productionPath) → LanguageEngineResult`;
`ResolveLanguageEngine(IEnumerable<ILanguageEngine> engines)` takes `Path.GetExtension(productionPath)`
and keeps engines whose `FileExtensions` contain it under `StringComparison.OrdinalIgnoreCase`.
**Context:** The production file is the one fact every `verify` call has (`--production`). Windows
paths are case-insensitive, so `Foo.CS` is C#.
**Alternatives rejected:** a `--language` flag (one more thing for the hook to get wrong); reading the
intent's `class` (not a language signal).

### ADR-3 — Failures are result values: `NoEngineForExtension`, `MultipleEnginesForExtension`
**Decision:** `LanguageEngineError { None, NoEngineForExtension, MultipleEnginesForExtension }`;
`LanguageEngineResult { bool IsResolved; LanguageEngineError Error; string Message; ILanguageEngine Engine }`.
Messages: `$"No language engine handles '{extension}' files: {productionPath}"` and
`$"More than one language engine handles '{extension}' files: {languages}"` (languages comma-joined).
A path with no extension reports `''` as the extension.
**Context:** `errors-and-logging.md` — predictable failures return an enum, never throw. Two engines
claiming one extension would otherwise resolve to whichever scanned first, which is not a contract.
**Alternatives rejected:** throwing; `FirstOrDefault` (silent, order-dependent pick).

### ADR-4 — No caller yet
**Decision:** Nothing calls `IResolveLanguageEngine` in Foundation. The first gate that needs an
engine injects it and turns an unresolved result into a failed `GateResult`.
**Context:** Keeps this item off `VerifyCommand.cs` / `VerificationContext`, which item 4 edits in
the same item wave.
**Alternatives rejected:** resolving in `VerifyCommand` and adding the engine to `VerificationContext`
now (collides with item 4, and every C# run would succeed trivially — no behaviour to verify yet).

## Assumptions

- Nullable disabled; `Engine` is null on an unresolved result (callers check `IsResolved`).
- `SystemScope` registers `CSharpLanguageEngine` as `ILanguageEngine` (it is public with a public
  constructor). Registration is not unit-tested (no container tests exist in the CLI test project);
  the first gate that consumes the seam proves it end to end.
- The task branch is `language-engine-seam`, checked out in its **own** working directory (a
  `git worktree`) so item 4 can be built at the same time.

## Open Questions

None blocking. For the human reviewer:

- **Registration proof.** If a container-level test is wanted before a gate consumes the seam, it
  would be the first of its kind in the CLI tests — a deliberate new pattern, so it is left out.
- **Extensions with multiple dots** (`.g.cs`) resolve on the last extension (`.cs`) — fine for C#.
