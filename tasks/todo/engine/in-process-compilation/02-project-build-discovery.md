# Phase 2 — Project Build Discovery + Source Gathering

- **Work item:** in-process-compilation (see `tasks/todo/engine/in-process-compilation/00-overview.md`)
- **Depends on:** Phase 1 (`01-compilation-contract.md`)
- **Depended on by:** Phase 5
- **Wave:** 2
- **Parallel with:** Phases 3 and 4. No dependency among them, disjoint write sets, no shared hotspot.
- **Risk:** **Medium** — the most branching in the item. A wrong pick silently compiles against
  the wrong build output (stale TFM or configuration).

## Touches

- Creates:
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/FindProjectBuild.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/GatherCompilationSources.cs`
  - `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharp/FindProjectBuildTests.cs`
  - `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharp/GatherCompilationSourcesTests.cs`
- Modifies: —
- Exclusive runtime resources: none

You run in your own worktree while Phase 3 (reference gathering) and Phase 4 (Roslyn compiler) add
other files under `Engines/CSharp/`. Create **only** the files above.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Then read `tasks/todo/engine/in-process-compilation/00-overview.md`, ADR-3 and ADR-5.

Current state you will find (Phase 1 committed):

- `Engines/EngineError.cs`:
  `{ None, ProjectNotFound, MultipleProjectsFound, ProjectFileUnreadable, TargetFrameworkNotFound, BuildOutputNotFound }`.
- `Engines/CSharp/ProjectBuild.cs`: `ProjectFilePath`, `ProjectDirectory`, `AssemblyName`,
  `TargetFramework`, `OutputDirectory`, `IntermediateDirectory`.
- `Engines/CSharp/ProjectBuildResult.cs`: `Error`, `Message`, `Build`.
- `Engines/CSharp/SourceFile.cs`: `FilePath`, `Text`.
- `FatCat.Toolkit` `IFileSystemTools` (`C:\Code\FatCat.Toolkit\src\ToolKit\FileSystemTools.cs`):
  - Listing: `bool FileExists(path)`, `List<string> GetFiles(dir)` and
    `List<string> GetDirectories(dir)`. Both return **full paths**, and an empty list when the
    directory is missing.
  - Metadata: `IFileInfo GetFileMetaData(path)` (`System.IO.Abstractions`; use `LastWriteTimeUtc`).
  - Reading: `Task<string> ReadAllText(path)` returns `""` for a missing file.
- Real layout this must handle (`CodeWorker.Cli.Tests`, `AssemblyName` `Testing.FatCat.CodeWorker.Cli`):
  - `bin/Debug/net10.0/` (stale) **and** `bin/Debug/net11.0/` both exist.
  - The implicit-usings file is `obj/Debug/net11.0/CodeWorker.Cli.Tests.GlobalUsings.g.cs`, named
    after the **project file**.
  - `GlobalUsings.cs` sits at the project root.
- The pattern to mirror for value-returning failures is `ResolveLanguageEngine.cs` (private
  `Failure(error, message)` helper, switch expressions).

## Design (build exactly this shape)

Namespace `FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp`. Interface and class share a file
(single implementation). Primary constructors.

**`FindProjectBuild.cs`:**
```csharp
public interface IFindProjectBuild
{
	Task<ProjectBuildResult> Find(string sourceFilePath);
}

public class FindProjectBuild(IFileSystemTools fileSystemTools) : IFindProjectBuild
```
Behavior (ADR-3):
1. **Find the project.** Start at `Path.GetDirectoryName(sourceFilePath)`. In each folder, take the
   `GetFiles` entries whose extension is `.csproj` (case-insensitive). Exactly one means found. Two
   or more → `MultipleProjectsFound` (message names the folder). None → move to the parent. No
   parent → `ProjectNotFound` (message names the source file).
2. **Parse the project file.** Read it with `ReadAllText` and parse it with
   `XDocument.Parse`. Catch **only** `XmlException` → `ProjectFileUnreadable` (message names the
   project file). An empty string also fails to parse, so a vanished file lands here too.
3. **Read the properties.**
   - `TargetFramework` is the value of the first element whose `Name.LocalName` is
     `TargetFramework` (trimmed). Missing or blank → `TargetFrameworkNotFound`. A project with only
     `TargetFrameworks` (plural) lands here too.
   - `AssemblyName` is the first `AssemblyName` element's value, otherwise
     `Path.GetFileNameWithoutExtension(projectFile)`.
4. **Choose the configuration.**
   - Candidates are `GetDirectories(<projectDir>/bin)`. A configuration qualifies when
     `<configDir>/<tfm>/<AssemblyName>.dll` passes `FileExists`.
   - Pick the qualifying configuration with the greatest `GetFileMetaData(dll).LastWriteTimeUtc`.
   - None → `BuildOutputNotFound` (message names the project, TFM and assembly).
5. **Return** `ProjectBuild` with `OutputDirectory = <configDir>/<tfm>` and
   `IntermediateDirectory = <projectDir>/obj/<configName>/<tfm>`. Build all paths with
   `Path.Combine`.

Keep each step a small, well-named private method. Use `ProjectBuildResult` with `Error = None` on
success.

**`GatherCompilationSources.cs`:**
```csharp
public interface IGatherCompilationSources
{
	Task<List<SourceFile>> Gather(string productionPath, string testsPath, ProjectBuild productionBuild, ProjectBuild testBuild);
}

public class GatherCompilationSources(IFileSystemTools fileSystemTools) : IGatherCompilationSources
```
Behavior (ADR-5): return, in order:
1. the production file;
2. the test file;
3. for each **distinct** project (`testBuild` first, then `productionBuild` when its
   `ProjectFilePath` differs, compared case-insensitively):
   - `<ProjectDirectory>/GlobalUsings.cs` if `FileExists`;
   - then `<IntermediateDirectory>/<Path.GetFileNameWithoutExtension(ProjectFilePath)>.GlobalUsings.g.cs`
     if `FileExists`.

Each `SourceFile` has the full path and the `ReadAllText` contents.

## Steps (TDD — tests first, red before green)

Fake `IFileSystemTools` with `A.Fake<IFileSystemTools>()`. Use generated paths built from
`Faker.Create<string>()` segments joined with `Path.Combine`. Pin only what each test depends on.
One assertion per test. A small helper that "creates" a project (`GetFiles` returns the csproj,
`ReadAllText` returns given XML) keeps tests short.

`FindProjectBuildTests` (at minimum):
- `FindTheProjectInTheSourceFilesFolder`
- `FindTheProjectInAnAncestorFolder`
- `ReturnProjectNotFoundWhenNoAncestorHasAProject`
- `ReturnMultipleProjectsFoundWhenAFolderHasTwoProjects`
- `IgnoreFilesThatAreNotProjects`
- `ReturnProjectFileUnreadableWhenTheXmlIsMalformed`
- `ReturnTargetFrameworkNotFoundWhenTheElementIsMissing`
- `ReturnTargetFrameworkNotFoundWhenOnlyTargetFrameworksIsSet`
- `ReadTheTargetFramework`
- `ReadTheAssemblyName`
- `DefaultTheAssemblyNameToTheProjectFileName`
- `UseTheOutputFolderForTheProjectsTargetFramework` (a stale other-TFM DLL must not qualify)
- `PickTheMostRecentlyBuiltConfiguration`
- `ReturnBuildOutputNotFoundWhenNoConfigurationHasTheAssembly`
- `SetTheIntermediateDirectoryForTheChosenConfiguration`
- `ReturnNoErrorWhenFound`

`GatherCompilationSourcesTests` (at minimum):
- `ReturnTheProductionFileFirst`
- `ReturnTheTestFileSecond`
- `IncludeTheTestProjectsGlobalUsingsFile`
- `IncludeTheTestProjectsGeneratedImplicitUsingsFile`
- `NameTheGeneratedFileAfterTheProjectFile`
- `SkipGlobalUsingsFilesThatDoNotExist`
- `IncludeTheProductionProjectsUsingsWhenItIsADifferentProject`
- `IncludeEachProjectsUsingsOnceWhenBothFilesShareAProject`
- `ReadEachSourcesText`

Red first, then implement to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (including the existing E2E suite)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; correct type-role suffixes; no banned
      patterns (no `System.IO.File`/`Directory` calls; all IO through `IFileSystemTools`)
- [ ] Only the files in `## Touches` were created (wave-2 disjointness)
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the isolated worktree branch the orchestrator gave you, message referencing this file; no push

Suggested commit message:

```
in-process-compilation phase 2: project build discovery + source gathering (tasks/todo/engine/in-process-compilation/02-project-build-discovery.md)
```

## Rollback Procedure

- Revert phase 5 first (it injects both interfaces), then `git revert <this phase's commit>`. No
  manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (every departure from
this plan and why — an empty log is a claim, not a default); open questions/risks for the reviewer.

## Hand-off

- **`IFindProjectBuild.Find(sourceFilePath) → Task<ProjectBuildResult>`.** On success it returns
  `Error = None` and a populated `ProjectBuild`. On failure it returns one of the five discovery
  `EngineError`s with a message naming the path.
- **`IGatherCompilationSources.Gather(productionPath, testsPath, productionBuild, testBuild) → Task<List<SourceFile>>`.**
  The production file comes first, then the test file, then the global usings for each distinct
  project.
