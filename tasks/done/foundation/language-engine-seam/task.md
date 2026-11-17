# Work Item

`language-engine-seam`

# Specification

Give the gates a **language engine seam**: one capability interface, `IResolveLanguageEngine`, that
hands a gate the right engine for the production file under verification. No gate hard-codes a
language (README "C#-first, language-agnostic by design").

Foundation ships the **seam, its registration point, and an identity-only C# engine** (user decision
2026-11-17): `CSharpLanguageEngine` declares only that it handles C# and the `.cs` extension. It has
no compile, test or mutation behaviour yet — the in-process Roslyn work is the separate `Engine`
group, which adds members to the engine contract when it has real work to put behind them. A second
language is deferred (Full).

Engines are discovered by `SystemScope` scanning (every `ILanguageEngine` implementation is
registered, as `IVerificationGate` implementations are), so adding an engine is a new class — no
gate, resolver or module change.

## Acceptance Criteria

- [ ] `ILanguageEngine` declares the engine's language name and the file extensions it handles.
- [ ] `CSharpLanguageEngine` reports language `C#` and the single extension `.cs`.
- [ ] `IResolveLanguageEngine.Resolve(productionPath)` returns the engine whose extensions include the
      file's extension, matched case-insensitively (`Foo.CS` resolves to the C# engine).
- [ ] An unsupported extension (or none) returns a **result value** with a `NoEngineForExtension`
      error and a message naming the extension and file — never an exception.
- [ ] Two engines claiming the same extension return a `MultipleEnginesForExtension` error value
      rather than an arbitrary pick (registration order is not a contract).
- [ ] The resolver depends only on `IEnumerable<ILanguageEngine>`, so it is unit-tested with fake
      engines and needs no change when an engine is added.
- [ ] Zero warnings; `dotnet test` green (including the existing E2E suite, unchanged).

## Out of Scope / Non-Goals

- Any compile / test / mutation members on the engine — the `Engine` group.
- Calling the resolver from `verify` or a gate — the first gate that needs an engine (Engine /
  Deterministic gates groups). No `VerifyCommand` or `VerificationContext` change here, which keeps
  this item clear of `verdict-model-reporting`'s hotspot.
- Choosing the engine from tool-level configuration — Integration group.
- A second-language engine — Full (deferred).

## Feature Context

- Epic plan: [`tasks/todo/foundation/00-epic-plan.md`](../../../todo/foundation/00-epic-plan.md) — item 5, ADR-G1/G5.
- Built on: [`gate-pipeline-framework`](../gate-pipeline-framework/00-overview.md) (item 3) — the
  multi-implementation scanning pattern (`IEnumerable<IVerificationGate>`) this seam reuses.
- Built alongside: [`verdict-model-reporting`](../verdict-model-reporting/00-overview.md) (item 4) —
  same item wave, disjoint files.
- Source of truth: [`CodeWorker.Cli/README.md`](../../../../CodeWorker.Cli/README.md) — "The engine
  runs in-process", "C#-first, language-agnostic by design".
