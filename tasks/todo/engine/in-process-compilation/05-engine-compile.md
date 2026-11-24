# Phase 5 — Engine `Compile` Member + Orchestration + Timed Smoke Run

- **Work item:** in-process-compilation (see `tasks/todo/engine/in-process-compilation/00-overview.md`)
- **Depends on:** Phases 1, 2, 3, 4
- **Depended on by:** —
- **Wave:** 3
- **Parallel with:** none. It joins all three wave-2 collaborators.
- **Risk:** **Medium** — it changes the seam's interface (`ILanguageEngine`) and gives
  `CSharpLanguageEngine` constructor dependencies. That ripples into two existing test classes.
  It is internal to the CLI, with no external API, auth or data, so it is not auto-high.

## Touches

- Modifies:
  - `CodeWorker.Cli/Commands/Verify/Engines/ILanguageEngine.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharpLanguageEngine.cs`
  - `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharpLanguageEngineTests.cs`
  - `CodeWorker.Cli.Tests/Commands/Verify/Engines/ResolveLanguageEngineTests.cs` (line ~172 constructs a
    real `CSharpLanguageEngine`)
- Creates: none in the repo. The smoke app lives in the session scratchpad and is **not committed**.
- Exclusive runtime resources: none

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Then read `tasks/todo/engine/in-process-compilation/00-overview.md` (ADR-1, ADR-7, ADR-8)
and `tasks/todo/engine/00-epic-plan.md`, ADR-G1 and ADR-G5. **Do not touch `VerifyCommand`, the
pipeline or the verdict.**

Current state you will find (phases 1–4 committed):

- `Engines/ILanguageEngine.cs` (standalone interface): `string Language`,
  `IReadOnlyList<string> FileExtensions`.
- `Engines/CSharpLanguageEngine.cs`: no constructor; `"C#"` / `[".cs"]`.
- `Engines/CompilationResult.cs`, `CompilationDiagnostic.cs`, `CompilationDiagnosticSeverity.cs`,
  `EngineError.cs` (phase 1).
- `Engines/CSharp/` contains:
  - `IFindProjectBuild.Find(sourceFilePath) → Task<ProjectBuildResult>` (phase 2)
  - `IGatherCompilationSources.Gather(productionPath, testsPath, productionBuild, testBuild) → Task<List<SourceFile>>` (phase 2)
  - `IGatherCompilationReferences.Gather(outputDirectory) → List<string>` (phase 3)
  - `ICompileInMemory.Compile(assemblyName, sources, referencePaths) → CompilationResult` (phase 4;
    always `Error = None`; `BuildOutputDirectory` left null)
- `ProjectBuildResult { Error, Message, Build }` and `ProjectBuild { ProjectFilePath, ProjectDirectory, AssemblyName, TargetFramework, OutputDirectory, IntermediateDirectory }`.
- `CSharpLanguageEngineTests` constructs `new CSharpLanguageEngine()` and asserts the identity members.
- `ResolveLanguageEngineTests` uses `new ResolveLanguageEngine([new CSharpLanguageEngine()])` once
  (around line 172) to prove the real engine resolves for `.cs`.
- DI: `SystemScope` scanning registers every public implementation. No module change.

## Design (build exactly this shape)

`ILanguageEngine` gains:
```csharp
Task<CompilationResult> Compile(string productionPath, string testsPath);
```

`CSharpLanguageEngine`:
```csharp
public class CSharpLanguageEngine(
	IFindProjectBuild findProjectBuild,
	IGatherCompilationSources gatherCompilationSources,
	IGatherCompilationReferences gatherCompilationReferences,
	ICompileInMemory compileInMemory
) : ILanguageEngine
```
`Compile` (ADR-7):
1. `testBuild = await findProjectBuild.Find(testsPath)`. If `Error != None`, return
   `new CompilationResult { Error = testBuild.Error, Message = testBuild.Message }`. Nothing else
   runs.
2. `productionBuild = await findProjectBuild.Find(productionPath)`. Same guard.
3. `sources = await gatherCompilationSources.Gather(productionPath, testsPath, productionBuild.Build, testBuild.Build)`.
4. `references = gatherCompilationReferences.Gather(testBuild.Build.OutputDirectory)`.
5. `result = compileInMemory.Compile($"{testBuild.Build.AssemblyName}.Verify", sources, references)`.
6. `result.BuildOutputDirectory = testBuild.Build.OutputDirectory`; return it.

Keep the guard-and-return shape with small private helpers (e.g. `EngineFailure(ProjectBuildResult)`),
no nesting.

## Steps (TDD — tests first, red before green)

**`CSharpLanguageEngineTests`.** Construct the engine with four fakes in the test constructor. Use
`current*` fields + `ReturnsLazily` for each `Find` result, keyed on the path argument (test path vs
production path). Default both to success with `Faker.Create<ProjectBuild>()`. Keep the three
existing identity tests green unchanged apart from construction. Add, one assertion each:

- `FindTheTestProjectBuild`
- `FindTheProductionProjectBuild`
- `ReturnTheTestProjectsEngineErrorWhenItCannotBeFound`
- `ReturnTheTestProjectsErrorMessageWhenItCannotBeFound`
- `NotCompileWhenTheTestProjectCannotBeFound`
- `ReturnTheProductionProjectsEngineErrorWhenItCannotBeFound`
- `NotCompileWhenTheProductionProjectCannotBeFound`
- `GatherSourcesForBothFilesAndBothProjects`
- `GatherReferencesFromTheTestProjectsBuildOutput`
- `CompileTheGatheredSourcesAndReferences`
- `NameTheAssemblyAfterTheTestAssemblyWithAVerifySuffix`
- `ReturnTheCompilersDiagnostics`
- `SetTheBuildOutputDirectoryToTheTestProjectsOutput`

**`ResolveLanguageEngineTests`.** Replace `new CSharpLanguageEngine()` with one built from four
`A.Fake<…>()`s. The test's intent (the real C# engine resolves for `.cs`) is unchanged.

Red first, then implement to green.

**Timed smoke run (manual, not committed; ADR-8):**
1. Run `dotnet build CodeWorker.slnx` so `CodeWorker.Cli.Tests/bin/Debug/net11.0/` is current.
2. In the session scratchpad (outside the repo), write `smoke.cs`:
   - `#:project <absolute path>/CodeWorker.Cli/CodeWorker.Cli.csproj`
   - In the body, call `SystemScope.Initialize(...)` with the same assemblies and module as
     `CodeWorker.Cli/Program.cs`.
   - Resolve `IResolveLanguageEngine` and get the engine for the production path.
   - Start a `Stopwatch` (fine here: throwaway code), then call `Compile` for:
     - production: `CodeWorker.Cli/Commands/Verify/Engines/ResolveLanguageEngine.cs`
     - tests: `CodeWorker.Cli.Tests/Commands/Verify/Engines/ResolveLanguageEngineTests.cs`
   - Print `Error`, the diagnostic count with each diagnostic, `AssemblyImage.Length`,
     `BuildOutputDirectory` and elapsed milliseconds.
3. Run it with `dotnet run smoke.cs` (a fresh process, so cold Roslyn) **twice**. Record both
   elapsed times.

**Expected:** `Error = None`, zero error diagnostics, a non-empty image, and `BuildOutputDirectory`
ending in `CodeWorker.Cli.Tests\bin\Debug\net11.0`.

**If the smoke run shows errors:** an overlay-caused diagnostic that phase 4's suppression list
missed, or a discovery/reference bug, is a **plan defect**. Fix it only if the fix lies inside this
phase's files. Otherwise report PHASE FAILED with the diagnostics, so the owning phase is amended.
Delete nothing from the repo; the scratch file is outside it.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (including the existing E2E suite)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; correct type-role suffixes; no banned patterns
- [ ] Timed smoke run passed (no errors, non-empty image); both elapsed times in the Phase Report
- [ ] No file outside `## Touches` changed; `smoke.cs` is not in the repo
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current task branch, message referencing this file; no push

Suggested commit message:

```
in-process-compilation phase 5: engine Compile member + orchestration (tasks/todo/engine/in-process-compilation/05-engine-compile.md)
```

## Rollback Procedure

- `git revert <this phase's commit>`. Nothing in this item depends on it. `ILanguageEngine` returns
  to identity-only and `CSharpLanguageEngine` to parameterless. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); **smoke run output and both cold
elapsed times** (judge them against the sub-10s budget: compile is the first of several steps);
deviation log (every departure from this plan and why — an empty log is a claim, not a default);
open questions/risks for the reviewer.

## Hand-off

- **`ILanguageEngine.Compile(productionPath, testsPath) → Task<CompilationResult>`**, reached
  through `IResolveLanguageEngine`. This is the entry point for the Compile gate (*Deterministic
  gates*) and item 2's test runner. Read the result as follows:
  - `Error != None` → the engine could not compile; `Message` says why.
  - `HasErrors` → the code does not compile (`Diagnostics` hold id, message, file, line, column).
  - Otherwise `AssemblyImage` is the overlay assembly named `<test AssemblyName>.Verify`, and
    `BuildOutputDirectory` is where its dependencies load from (item 2).
