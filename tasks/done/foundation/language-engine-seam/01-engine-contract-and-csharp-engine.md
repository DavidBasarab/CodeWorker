# Phase 1 — Engine Contract + Identity-Only C# Engine

- **Work item:** language-engine-seam (see `tasks/todo/foundation/language-engine-seam/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 2
- **Wave:** 1
- **Parallel with:** none within this item (Phase 2 needs this interface). Across items it runs
  beside `verdict-model-reporting` — disjoint files (everything here is under `Engines/`).
- **Risk:** **Low** — two new types; nothing consumes them yet.

## Touches

- Creates: `CodeWorker.Cli/Commands/Verify/Engines/ILanguageEngine.cs`,
  `CodeWorker.Cli/Commands/Verify/Engines/CSharpLanguageEngine.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharpLanguageEngineTests.cs`
- Modifies: nothing
- Exclusive runtime resources: none

Phases of another item run at the same time in their own worktrees. Create or modify **only** these files.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("The engine runs in-process", "C#-first, language-agnostic by
design"), `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first — mandatory (block bodies even
for properties, `[]` collection expressions, one type per file, no comments explaining code).

**Worktree base:** before reading code, run `git merge --ff-only <wave base>` (the commit the
orchestrator names) in your still-clean worktree, then confirm `git rev-parse HEAD` equals it — the
worktree tool starts from `main`.

Current state: `CodeWorker.Cli/Commands/Verify/` has `Intent/` and `Gates/`; there is no `Engines/`
folder. The precedent for a multi-implementation interface in its own file is
`Gates/IVerificationGate.cs`:
```csharp
namespace FatCat.CodeWorker.Cli.Commands.Verify.Gates;

public interface IVerificationGate
{
	string Name { get; }

	GateStage Stage { get; }

	Task<GateResult> Run(VerificationContext context);
}
```
`SystemScope` scanning registers every public implementation, so no module change is needed.

## Design (build exactly this shape)

Folder `CodeWorker.Cli/Commands/Verify/Engines/`, namespace `FatCat.CodeWorker.Cli.Commands.Verify.Engines`.

`ILanguageEngine.cs` (standalone — it will have several implementations):
```csharp
public interface ILanguageEngine
{
	string Language { get; }

	IReadOnlyList<string> FileExtensions { get; }
}
```

`CSharpLanguageEngine.cs` — identity only (user decision 2026-11-17; the `Engine` group adds real
members later):
```csharp
public class CSharpLanguageEngine : ILanguageEngine
{
	public string Language
	{
		get { return "C#"; }
	}

	public IReadOnlyList<string> FileExtensions
	{
		get { return [".cs"]; }
	}
}
```
Extensions include the leading dot (they are compared with `Path.GetExtension` output in Phase 2).

## Steps (TDD — tests first, red before green)

`CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharpLanguageEngineTests.cs`, namespace
`Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines`; SUT `new CSharpLanguageEngine()` in the
constructor. One assertion each:

1. `ReportCSharpAsItsLanguage` — `Language` is `"C#"`.
2. `HandleTheCsExtension` — `FileExtensions` contains `".cs"`.
3. `HandleOnlyOneExtension` — `FileExtensions` has exactly one entry.

Red first (the types do not exist), then implement to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (existing E2E unchanged)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one type per file; no expression-bodied members; no banned patterns
- [ ] Only the files in `## Touches` created/modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review` (screen-only — write nothing outside `## Touches`)
- [ ] Exactly one commit on your worktree branch, message referencing this file; no push

Suggested commit message:

```
language-engine-seam phase 1: engine contract + identity-only C# engine (tasks/todo/foundation/language-engine-seam/01-engine-contract-and-csharp-engine.md)
```

## Rollback Procedure

- Revert Phase 2 first if committed, then `git revert <this commit>`. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (an empty log is a claim,
not a default); open questions/risks.

## Hand-off

- `ILanguageEngine { string Language; IReadOnlyList<string> FileExtensions }` (namespace
  `FatCat.CodeWorker.Cli.Commands.Verify.Engines`) — Phase 2's resolver consumes
  `IEnumerable<ILanguageEngine>`.
- `CSharpLanguageEngine` — `Language` `"C#"`, `FileExtensions` `[".cs"]` (leading dot).
