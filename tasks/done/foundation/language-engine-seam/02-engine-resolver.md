# Phase 2 — Engine Resolver

- **Work item:** language-engine-seam (see `tasks/todo/foundation/language-engine-seam/00-overview.md`)
- **Depends on:** Phase 1
- **Depended on by:** —
- **Wave:** 2
- **Parallel with:** none within this item. Across items it runs beside `verdict-model-reporting` —
  disjoint files (everything here is under `Engines/`).
- **Risk:** **Medium** — defines the seam every later gate resolves its engine through.

## Touches

- Creates: `CodeWorker.Cli/Commands/Verify/Engines/LanguageEngineError.cs`,
  `CodeWorker.Cli/Commands/Verify/Engines/LanguageEngineResult.cs`,
  `CodeWorker.Cli/Commands/Verify/Engines/ResolveLanguageEngine.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/Engines/ResolveLanguageEngineTests.cs`
- Modifies: nothing
- Exclusive runtime resources: none

Phases of another item run at the same time in their own worktrees. Create or modify **only** these files.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("C#-first, language-agnostic by design"), `CLAUDE.md`, and **all**
`.claude/rules/csharp/*.md` first — mandatory. Especially `errors-and-logging.md` (predictable
failures return an enum, never throw) and `types-and-di.md` (LINQ method chains, `var`).

**Worktree base:** before reading code, run `git merge --ff-only <wave base>` (the commit the
orchestrator names) in your still-clean worktree, then confirm `git rev-parse HEAD` equals it.

Built by Phase 1 (namespace `FatCat.CodeWorker.Cli.Commands.Verify.Engines`):
`ILanguageEngine { string Language { get; } IReadOnlyList<string> FileExtensions { get; } }` and
`CSharpLanguageEngine` (`"C#"`, `[".cs"]`).

Result-value precedent to mirror (`Commands/Verify/Intent/`): `IntentResult { bool IsValid;
IntentValidationError Error; string Message; IntentContract Intent }` with `IntentValidationError`
starting at `None`. Multi-implementation injection precedent: `RunGatePipeline(IEnumerable<IVerificationGate> gates, IClock clock)`.

## Design (build exactly this shape)

```csharp
public enum LanguageEngineError
{
	None,
	NoEngineForExtension,
	MultipleEnginesForExtension,
}

public class LanguageEngineResult
{
	public bool IsResolved { get; set; }
	public LanguageEngineError Error { get; set; }
	public string Message { get; set; }
	public ILanguageEngine Engine { get; set; }
}
```

`ResolveLanguageEngine.cs` — interface + class in one file:
```csharp
public interface IResolveLanguageEngine
{
	LanguageEngineResult Resolve(string productionPath);
}

public class ResolveLanguageEngine(IEnumerable<ILanguageEngine> engines) : IResolveLanguageEngine
```
- `var extension = Path.GetExtension(productionPath);` (`""` when there is none).
- Matching engines: `engines.Where(engine => engine.FileExtensions.Any(handled => string.Equals(handled, extension, StringComparison.OrdinalIgnoreCase)))`, materialized once.
- Exactly one → `IsResolved = true`, `Error = None`, `Engine` = it.
- None → `NoEngineForExtension`, `Message = $"No language engine handles '{extension}' files: {productionPath}"`.
- More than one → `MultipleEnginesForExtension`,
  `Message = $"More than one language engine handles '{extension}' files: {string.Join(", ", languages)}"`.
- Never throws for these cases. Small named private methods (`Resolved`, `NoEngine`, `MultipleEngines`); no comments.

## Steps (TDD — tests first, red before green)

`ResolveLanguageEngineTests` (namespace `Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines`).
Use `A.Fake<ILanguageEngine>()` engines whose `Language` / `FileExtensions` are configured per test
(e.g. a fake C# engine `[".cs"]` and a fake TypeScript engine `[".ts", ".tsx"]`); construct the SUT
with the list the test needs. Paths may be fixed strings like `@"C:\work\Foo.cs"` — the extension is
what the test pins. One assertion each:

1. `ResolveTheEngineThatHandlesTheExtension` — `Engine` is the fake C# engine.
2. `ReportResolvedWhenOneEngineMatches` — `IsResolved` true.
3. `ReportNoErrorWhenOneEngineMatches` — `Error` is `None`.
4. `MatchTheExtensionIgnoringCase` — `Foo.CS` resolves the C# engine.
5. `ChooseAmongSeveralEnginesByExtension` — `Foo.tsx` resolves the TypeScript engine.
6. `ReturnNoEngineForAnUnsupportedExtension` — `Foo.py` → `NoEngineForExtension`.
7. `ReportNotResolvedForAnUnsupportedExtension` — `IsResolved` false.
8. `NameTheExtensionAndFileWhenNoEngineMatches` — exact message.
9. `ReturnNoEngineWhenTheFileHasNoExtension` — `Makefile` → `NoEngineForExtension`.
10. `ReturnNoEngineWhenNoEnginesAreRegistered` — empty list → `NoEngineForExtension`.
11. `ReturnMultipleEnginesWhenTwoClaimTheExtension` — two fakes with `[".cs"]` → `MultipleEnginesForExtension`.
12. `NameTheCompetingLanguagesWhenSeveralMatch` — message contains both languages.
13. `ResolveTheRealCSharpEngineForACsFile` — SUT built with `[new CSharpLanguageEngine()]`; `Engine.Language` is `"C#"` (proves the Phase 1 engine plugs into the seam).

Red first, then implement to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (existing E2E unchanged)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one type per file; LINQ method syntax; no exceptions for predictable failures; no banned patterns
- [ ] Only the files in `## Touches` created/modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review` (screen-only — write nothing outside `## Touches`)
- [ ] Exactly one commit on your worktree branch, message referencing this file; no push

Suggested commit message:

```
language-engine-seam phase 2: engine resolver (tasks/todo/foundation/language-engine-seam/02-engine-resolver.md)
```

## Rollback Procedure

- `git revert <this commit>`. Nothing consumes the resolver yet. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (an empty log is a claim,
not a default); open questions/risks.

## Hand-off

- **The seam:** inject `IResolveLanguageEngine` and call `Resolve(context.ProductionPath)`. Check
  `IsResolved`; when false, turn `Message` into a failed `GateResult` finding (the first engine-backed
  gate decides the exact shape).
- **Adding an engine:** a new public class implementing `ILanguageEngine` — `SystemScope` registers
  it; no resolver, gate or module change.
