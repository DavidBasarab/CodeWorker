# Phase 1 — Compilation Contract + Roslyn Package

- **Work item:** in-process-compilation (see `tasks/todo/engine/in-process-compilation/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phases 2, 3, 4, 5
- **Wave:** 1
- **Parallel with:** none. This is the contract phase every later phase builds against, and it owns
  the `*.csproj` hotspot.
- **Risk:** **Low** — new value types and enums plus one package reference; nothing is wired or
  called.

## Touches

- Creates:
  - `CodeWorker.Cli/Commands/Verify/Engines/EngineError.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CompilationDiagnosticSeverity.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CompilationDiagnostic.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CompilationResult.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/ProjectBuild.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/ProjectBuildResult.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/SourceFile.cs`
  - `CodeWorker.Cli.Tests/Commands/Verify/Engines/CompilationResultTests.cs`
- Modifies: `CodeWorker.Cli/CodeWorker.Cli.csproj` (add the Roslyn package)
- Exclusive runtime resources: none

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Then read `tasks/todo/engine/00-epic-plan.md` (ADR-G1..G5) and this item's
`00-overview.md` (ADR-2 especially).

Current state you will find:

- `CodeWorker.Cli/Commands/Verify/Engines/` has `ILanguageEngine.cs`, `CSharpLanguageEngine.cs`,
  `ResolveLanguageEngine.cs`, `LanguageEngineResult.cs` and `LanguageEngineError.cs`. Copy the
  shape of `LanguageEngineResult` + `LanguageEngineError`: plain class with auto-properties, an
  enum whose first member is `None`, one type per file, file-scoped namespace.
- There is no `Engines/CSharp/` folder yet. Its namespace is
  `FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp`.
- `CodeWorker.Cli/CodeWorker.Cli.csproj` lists package references with `Version="*"` (except
  CSharpier.MsBuild, which is pinned).

This phase defines data only. Do **not** add a member to `ILanguageEngine` (phase 5) and do not
create any interface or service class (phases 2–4).

## Design (build exactly this shape)

All classes are plain (records are banned), use auto-properties, collection expressions, block
bodies only, and no nullable reference annotations.

`Engines/EngineError.cs`:
```csharp
namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public enum EngineError
{
	None,
	ProjectNotFound,
	MultipleProjectsFound,
	ProjectFileUnreadable,
	TargetFrameworkNotFound,
	BuildOutputNotFound,
}
```

`Engines/CompilationDiagnosticSeverity.cs`: `public enum CompilationDiagnosticSeverity { Warning, Error }`

`Engines/CompilationDiagnostic.cs`: `Id` (string), `Message` (string), `Severity`
(`CompilationDiagnosticSeverity`), `FilePath` (string), `Line` (`int?`, 1-based), `Column` (`int?`,
1-based).

`Engines/CompilationResult.cs`:
```csharp
public class CompilationResult
{
	public EngineError Error { get; set; }

	public string Message { get; set; }

	public List<CompilationDiagnostic> Diagnostics { get; set; } = [];

	public byte[] AssemblyImage { get; set; } = [];

	public string BuildOutputDirectory { get; set; }

	public bool HasErrors
	{
		get { return Diagnostics.Any(diagnostic => diagnostic.Severity == CompilationDiagnosticSeverity.Error); }
	}
}
```

`Engines/CSharp/ProjectBuild.cs`: `ProjectFilePath`, `ProjectDirectory`, `AssemblyName`,
`TargetFramework`, `OutputDirectory` (`bin/<config>/<tfm>`), `IntermediateDirectory`
(`obj/<config>/<tfm>`), all strings.

`Engines/CSharp/ProjectBuildResult.cs`: `EngineError Error`, `string Message`, `ProjectBuild Build`
(mirrors `LanguageEngineResult`).

`Engines/CSharp/SourceFile.cs`: `FilePath`, `Text`, both strings.

`CodeWorker.Cli.csproj`: add `<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="*" />`
to the existing package `ItemGroup`. After restore, confirm the resolved version is **≥ 5.12.0**
(`dotnet list CodeWorker.Cli/CodeWorker.Cli.csproj package`). The installed SDK's compiler is the
5.12 line. If restore resolves lower, pin `Version="5.12.0"` and log it as a deviation.

## Steps (TDD — tests first, red before green)

1. Create `CompilationResultTests` (namespace `Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines`)
   with one assertion each:
   - `ReportNoErrorsWhenThereAreNoDiagnostics`
   - `ReportNoErrorsWhenOnlyWarningsArePresent`
   - `ReportErrorsWhenAnyDiagnosticIsAnError`
   - `StartWithAnEmptyAssemblyImage`
   - `StartWithNoDiagnostics`

   Build diagnostics with `Faker.Create<CompilationDiagnostic>()` and pin only `Severity`.
2. Watch them fail to compile/red, then add the types and make them green.
3. Add the package reference, then `dotnet restore` and `dotnet build CodeWorker.slnx`. Zero warnings.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (including the existing E2E suite)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; correct type-role suffixes; no banned patterns
- [ ] Only the files in `## Touches` were created or modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current task branch, message referencing this file; no push

Suggested commit message:

```
in-process-compilation phase 1: compilation contract + Roslyn package (tasks/todo/engine/in-process-compilation/01-compilation-contract.md)
```

## Rollback Procedure

- Revert phases 5, 4, 3 and 2 first (all depend on this), then `git revert <this phase's commit>`.
  No manual steps. Restore drops the package on the next build.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); the resolved
`Microsoft.CodeAnalysis.CSharp` version; deviation log (every departure from this plan and why —
an empty log is a claim, not a default); open questions/risks for the reviewer.

## Hand-off

- **`FatCat.CodeWorker.Cli.Commands.Verify.Engines`:**
  - `EngineError`
  - `CompilationDiagnosticSeverity`
  - `CompilationDiagnostic`
  - `CompilationResult` (with computed `HasErrors`)
- **`FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp`:**
  - `ProjectBuild`
  - `ProjectBuildResult`
  - `SourceFile`
- **`Microsoft.CodeAnalysis.CSharp`** is available to the CLI and, transitively, the test project.
- Phases 2, 3 and 4 run in parallel next and each create only their own files. Phase 5 adds
  `ILanguageEngine.Compile`.
