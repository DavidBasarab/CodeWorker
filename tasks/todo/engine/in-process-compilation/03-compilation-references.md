# Phase 3 — Reference Gathering

- **Work item:** in-process-compilation (see `tasks/todo/engine/in-process-compilation/00-overview.md`)
- **Depends on:** Phase 1 (`01-compilation-contract.md`)
- **Depended on by:** Phase 5
- **Wave:** 2
- **Parallel with:** Phases 2 and 4. No dependency among them, disjoint write sets, no shared hotspot.
- **Risk:** **Medium** — one duplicate-identity or native reference breaks every compile.

## Touches

- Creates:
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/GatherCompilationReferences.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/ReadTrustedPlatformAssemblies.cs`
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/InspectAssemblyFile.cs`
  - `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharp/GatherCompilationReferencesTests.cs`
- Modifies: —
- Exclusive runtime resources: none

You run in your own worktree while Phase 2 (discovery + sources) and Phase 4 (Roslyn compiler) add
other files under `Engines/CSharp/`. Create **only** the files above.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Read `testing.md`, "Low-Level API Implementations — No Unit Tests Required", carefully.
Then read `tasks/todo/engine/in-process-compilation/00-overview.md`, ADR-4.

Current state you will find (Phase 1 committed):

- `Engines/CSharp/` holds `ProjectBuild.cs`, `ProjectBuildResult.cs` and `SourceFile.cs`
  (namespace `FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp`). This phase does not need them.
  It receives the build output folder as a string.
- `FatCat.Toolkit` `IFileSystemTools.GetFiles(dir)` returns **full paths** of the folder's files
  (empty list when the folder is missing).
- Why each rule exists:
  - The **trusted platform assemblies** (TPA) list of the running process contains the .NET shared
    framework **and** the CLI's own dependencies (Roslyn, FatCat.Toolkit, Serilog, Autofac). Only
    entries under the runtime directory (`RuntimeEnvironment.GetRuntimeDirectory()`, i.e.
    `…/shared/Microsoft.NETCore.App/<version>/`) are framework assemblies.
  - A build output folder can contain **native** DLLs. Referencing one makes Roslyn fail with CS0009.
  - A build output can carry a newer package copy of a framework assembly (same file name). Keep
    the build output copy and drop the framework one, or Roslyn reports a duplicate identity.
- The house example of an `[ExcludeFromCodeCoverage]` wrapper is `CodeWorker.Cli/Time/SystemClock.cs`.

## Design (build exactly this shape)

Namespace `FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp`. Interface and class share a file.

**`ReadTrustedPlatformAssemblies.cs`** — low-level, excluded from coverage:
```csharp
public interface IReadTrustedPlatformAssemblies
{
	string RuntimeDirectory { get; }

	List<string> Read();
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over AppContext TRUSTED_PLATFORM_ASSEMBLIES and RuntimeEnvironment.GetRuntimeDirectory — no business logic, tested via IReadTrustedPlatformAssemblies fakes in GatherCompilationReferences."
)]
public class ReadTrustedPlatformAssemblies : IReadTrustedPlatformAssemblies
```
- `Read()` splits `(string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")` on
  `Path.PathSeparator`, dropping empty entries.
- `RuntimeDirectory` returns `RuntimeEnvironment.GetRuntimeDirectory()`.
- **No filtering here.** Filtering is branching logic and belongs in the tested class.

**`InspectAssemblyFile.cs`** — low-level, excluded from coverage:
```csharp
public interface IInspectAssemblyFile
{
	bool IsManagedAssembly(string path);
}
```
- Opens the file read-only, constructs `new PEReader(stream)` and returns `peReader.HasMetadata`.
- A `BadImageFormatException` returns `false`. That is a predictable "not a PE file" outcome, so it
  returns a value rather than swallowing anything.
- Justification: "Direct wrapper over System.Reflection.PortableExecutable.PEReader and a FileStream
  — no business logic, tested via IInspectAssemblyFile fakes in GatherCompilationReferences."

**`GatherCompilationReferences.cs`** — the tested logic:
```csharp
public interface IGatherCompilationReferences
{
	List<string> Gather(string outputDirectory);
}

public class GatherCompilationReferences(
	IFileSystemTools fileSystemTools,
	IReadTrustedPlatformAssemblies readTrustedPlatformAssemblies,
	IInspectAssemblyFile inspectAssemblyFile
) : IGatherCompilationReferences
```
Behavior (ADR-4):
1. **Build output assemblies.** Take `GetFiles(outputDirectory)` where the extension is `.dll`
   (case-insensitive) and `IsManagedAssembly` is true.
2. **Framework assemblies.** Take `Read()` entries that start with `RuntimeDirectory`
   (case-insensitive, `OrdinalIgnoreCase`). Leave out entries whose `Path.GetFileName` matches any
   build output assembly's file name (case-insensitive).
3. **Return** build output assemblies first, then framework assemblies.

## Steps (TDD — tests first, red before green)

`GatherCompilationReferencesTests`:
- Fake all three dependencies.
- Configure `GetFiles` / `Read()` / `RuntimeDirectory` through `ReturnsLazily` over fields set per
  test.
- Default `IsManagedAssembly` to `true`. A test opts specific paths out.

One assertion per test:
- `IncludeManagedAssembliesFromTheBuildOutput`
- `ExcludeNativeDllsFromTheBuildOutput`
- `ExcludeFilesThatAreNotDlls`
- `IncludeFrameworkAssembliesFromTheRuntimeDirectory`
- `ExcludeTrustedAssembliesOutsideTheRuntimeDirectory` (e.g. a Serilog path from the CLI's own folder)
- `PreferTheBuildOutputCopyOverTheFrameworkCopy`
- `MatchFileNamesWithoutRegardToCase`
- `ListBuildOutputAssembliesBeforeFrameworkAssemblies`
- `ReturnOnlyFrameworkAssembliesWhenTheBuildOutputIsEmpty`

Red first, then implement all three files to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (including the existing E2E suite)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; correct type-role suffixes; no banned patterns
- [ ] The two wrappers contain no branching beyond the `BadImageFormatException` → `false` mapping,
      and carry specific `Justification`s
- [ ] Only the files in `## Touches` were created (wave-2 disjointness)
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the isolated worktree branch the orchestrator gave you, message referencing this file; no push

Suggested commit message:

```
in-process-compilation phase 3: compilation reference gathering (tasks/todo/engine/in-process-compilation/03-compilation-references.md)
```

## Rollback Procedure

- Revert phase 5 first (it injects `IGatherCompilationReferences`), then
  `git revert <this phase's commit>`. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (every departure from
this plan and why — an empty log is a claim, not a default); open questions/risks for the reviewer.

## Hand-off

- **`IGatherCompilationReferences.Gather(outputDirectory) → List<string>`.** Returns full reference
  paths: managed build-output DLLs first, then the framework assemblies not shadowed by them.
- `IReadTrustedPlatformAssemblies` and `IInspectAssemblyFile` are internal collaborators. Item 2
  (test runner) may reuse `IReadTrustedPlatformAssemblies` if it needs the runtime directory.
