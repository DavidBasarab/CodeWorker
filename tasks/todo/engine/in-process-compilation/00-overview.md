# in-process-compilation — Overview

- **Work item:** `in-process-compilation` (Engine item 1; see
  [`tasks/todo/engine/00-epic-plan.md`](../00-epic-plan.md))
- **Source of truth:** `CodeWorker.Cli/README.md` ("The engine runs in-process")
- **Generated:** 2026-11-24-112457

## Work Item

Add `Compile(productionPath, testsPath)` to `ILanguageEngine` and implement it in
`CSharpLanguageEngine`. The implementation has four steps:

1. Find each file's owning project and its build output.
2. Gather the overlay source set.
3. Gather the reference set.
4. Compile in memory with Roslyn.

The result is a language-neutral `CompilationResult`. See `task.md` for the specification and
acceptance criteria.

### Current state that shapes the design (verified against the code, 2026-11-24)

- **The seam** (`CodeWorker.Cli/Commands/Verify/Engines/`):
  - `ILanguageEngine.cs` is a standalone interface with `string Language` and
    `IReadOnlyList<string> FileExtensions`.
  - `CSharpLanguageEngine.cs` has no constructor and returns `"C#"` / `[".cs"]`.
  - `ResolveLanguageEngine.cs` and `LanguageEngineResult.cs` (`IsResolved`, `Error`, `Message`,
    `Engine`), plus `LanguageEngineError` `{ None, NoEngineForExtension, MultipleEnginesForExtension }`.
    This is the error-as-value shape to mirror.
- **Tests that construct the engine directly:**
  - `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharpLanguageEngineTests.cs` (`new CSharpLanguageEngine()`).
  - `ResolveLanguageEngineTests.cs` line 172 (`new ResolveLanguageEngine([new CSharpLanguageEngine()])`).
  - Both break when the engine gains constructor dependencies (phase 5).
- **No Roslyn.** `CodeWorker.Cli.csproj` references CSharpier.MsBuild, FatCat.Toolkit
  (`Version="*"`), Serilog and Serilog.Sinks.Console. Published `Microsoft.CodeAnalysis.CSharp`
  versions run up to **5.12.0**. The installed SDK 11.0.100 ships compiler `5.12.0-1.26529.118`, so
  5.12.0 is the same compiler line and parses everything the repo compiles.
- **`IFileSystemTools`** (`C:\Code\FatCat.Toolkit\src\ToolKit\FileSystemTools.cs`):
  - Listing: `FileExists`, `DirectoryExists`, `GetFiles(dir)` and `GetDirectories(dir)`. Both
    return full paths, and an empty list when the directory is missing.
  - Metadata: `GetFileMetaData(path)` returns a `System.IO.Abstractions` `IFileInfo` with
    `LastWriteTimeUtc`.
  - Reading: `Task<string> ReadAllText(path)` returns `""` for a missing file (no exception).
- **Real build layout** (`CodeWorker.Cli.Tests`, `AssemblyName` `Testing.FatCat.CodeWorker.Cli`):
  - `bin/Debug/` holds both `net10.0/` (stale) and `net11.0/`.
  - The implicit-usings file is `obj/Debug/net11.0/CodeWorker.Cli.Tests.GlobalUsings.g.cs`. It is
    named after the **project file**, not the assembly, and contains `global using System;` …
    `System.Threading.Tasks;`.
  - `GlobalUsings.cs` at the project root adds `Xunit`, `FakeItEasy`, `FatCat.*`.
- **DI:** `SystemScope` scanning registers every public implementation, so no module change is
  needed (Foundation epic ADR-G5, corrected).

## Acceptance Criteria → Phase Map

| Acceptance criterion (task.md) | Proven by |
|---|---|
| `ILanguageEngine.Compile` member; identity unchanged | Phase 5 |
| Owning `.csproj` found by walking up | Phase 2 |
| Build output: `bin/<config>/<tfm>/<AssemblyName>.dll`, newest config wins, `AssemblyName` default | Phase 2 |
| Distinct `EngineError` per discovery failure; `Compile` stops before compiling | Phase 1 (enum), Phase 2 (returns), Phase 5 (stops) |
| Source set: production + test + each project's `GlobalUsings.cs` and `*.GlobalUsings.g.cs` | Phase 2 |
| Reference set: managed DLLs in build output + framework assemblies, build output wins, no natives | Phase 3 |
| Clean unit → no errors, non-empty image | Phase 4 (unit), Phase 5 (smoke) |
| Compile error → id, message, file, 1-based line/column; empty image; `HasErrors`; `Error` `None` | Phase 1 (`HasErrors`), Phase 4 |
| Warnings returned; hidden/info dropped | Phase 4 |
| Overlay-caused diagnostics suppressed | Phase 4 |
| Assembly named `<test AssemblyName>.Verify`; result carries build output directory | Phase 5 |
| Timed cold smoke run recorded | Phase 5 |
| Unit-tested; zero warnings; all green | Every phase + the integrated build after each wave |

## Phases & Dependency Graph

| Phase | File | Risk | Depends on | Depended on by |
|---|---|---|---|---|
| 1 — Compilation contract + Roslyn package (`CompilationResult`, `CompilationDiagnostic`, `CompilationDiagnosticSeverity`, `EngineError`, `CSharp/ProjectBuild`, `CSharp/ProjectBuildResult`, `CSharp/SourceFile`; `Microsoft.CodeAnalysis.CSharp` package reference) | `01-compilation-contract.md` | **Low** — new value types and a package reference; nothing wired | — | 2, 3, 4, 5 |
| 2 — Project build discovery + source gathering (`FindProjectBuild`, `GatherCompilationSources`) | `02-project-build-discovery.md` | **Medium** — most branching in the item; wrong discovery silently compiles against the wrong output | 1 | 5 |
| 3 — Reference gathering (`GatherCompilationReferences` + two low-level wrappers) | `03-compilation-references.md` | **Medium** — a duplicate or native reference breaks every compile | 1 | 5 |
| 4 — In-memory Roslyn compiler (`CompileInMemory`) | `04-roslyn-compiler.md` | **Medium** — first Roslyn use; diagnostic mapping and suppressions are the contract the Compile gate reads | 1 | 5 |
| 5 — Engine `Compile` member + orchestration + timed smoke run | `05-engine-compile.md` | **Medium** — changes the seam's public interface (`ILanguageEngine`); internal to the CLI, no external API, so not auto-high | 1, 2, 3, 4 | — |

**Revert cascade (reverse dependency order):** revert 5 first. Phases 2, 3 and 4 are independent
of each other, so revert any of them alone once 5 is reverted. Reverting 1 requires reverting 2–5
first.

### Execution waves (parallel subagents)

| Wave | Phases | Runs as | Why parallel-safe |
|---|---|---|---|
| 1 | 1 | Single subagent on the task branch | — (contract phase; also owns the `*.csproj` hotspot) |
| 2 | 2, 3, 4 | **Three parallel subagents** (the cap), each in its own worktree; commits cherry-picked in phase order | No dependency among them (each depends only on 1); disjoint write sets (below); no shared hotspot: the csproj edit was pulled into phase 1, and none edits `*Module.cs`, `GlobalUsings.cs`, README, `epics.md` or the E2E harness |
| 3 | 5 | Single subagent on the task branch | — (needs all three collaborators) |

**Critical path:** 1 → 2 → 5 (three sequential steps). Any wave-2 phase can stand in for 2.

Wave 2 file sets (disjoint, proven):

| Phase | Creates | Modifies |
|---|---|---|
| 2 | `CodeWorker.Cli/Commands/Verify/Engines/CSharp/FindProjectBuild.cs`, `…/CSharp/GatherCompilationSources.cs`, `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharp/FindProjectBuildTests.cs`, `…/CSharp/GatherCompilationSourcesTests.cs` | — |
| 3 | `…/CSharp/GatherCompilationReferences.cs`, `…/CSharp/ReadTrustedPlatformAssemblies.cs`, `…/CSharp/InspectAssemblyFile.cs`, `CodeWorker.Cli.Tests/…/CSharp/GatherCompilationReferencesTests.cs` | — |
| 4 | `…/CSharp/CompileInMemory.cs`, `CodeWorker.Cli.Tests/…/CSharp/CompileInMemoryTests.cs` | — |

**Why this split maximizes parallelism.** The four collaborators only meet at the engine. So the
plan:

- lands the values they exchange (`ProjectBuild`, `SourceFile`, `CompilationResult`, …) plus the
  Roslyn package in one small contract phase;
- fans three phases out against that contract (discovery with sources, references, compiler);
- joins them in phase 5.

**Splits considered and rejected:**

- **Source gathering as its own phase.** It shares discovery's `ProjectBuild` input and is small. As
  a separate fourth wave-2 phase it would exceed the 3-subagent cap and add a launch batch without
  shortening the critical path. It rides with discovery.
- **Adding `ILanguageEngine.Compile` in phase 1.** `CSharpLanguageEngine` would then need a stub
  implementation in the contract phase. It goes in phase 5 with the real implementation.
- **Pulling the csproj edit out of phase 1.** Phase 4 needs the package, and the csproj is a shared
  hotspot, so it belongs in the contract phase.

**Integration check after wave 2.** Each phase is proven alone, and none of them is wired into DI
consumers yet. So the integrated `dotnet build` + `dotnet test` after the cherry-picks mainly
proves the three new folders' files coexist: same namespace, no duplicate type names.

## Orchestrator

`orchestrator.md` (this folder) is the runbook. Say **"run in-process-compilation"**. It runs:

1. wave 1 (phase 1);
2. wave 2 (phases 2, 3 and 4 as parallel worktree subagents), cherry-picked in phase order, with
   the integration gate;
3. wave 3 (phase 5).

A watchdog stays armed throughout, and the run halts on failure. It never squashes, amends,
rebases or pushes.

## Decisions (lightweight ADRs)

### ADR-1 — The seam member: `Task<CompilationResult> Compile(string productionPath, string testsPath)`
**Decision:** Add to `ILanguageEngine`:
```csharp
Task<CompilationResult> Compile(string productionPath, string testsPath);
```
It takes paths, not a `VerificationContext`. The Engines namespace must not depend on Gates, and the
gate that calls it already holds both paths.
**Context:** Engine epic ADR-G1. The paths are what `VerificationContext` carries and what the
README says the AI submits. Async because discovery and source reading do file IO.
**Alternatives rejected:**
- `Compile(VerificationContext)`: couples Engines → Gates; a future second-language engine would
  depend on gate types.
- Source text instead of paths: mutation will need that, but as a C#-engine concern (re-emitting a
  rewritten syntax tree from the same compilation). Shaping it now is speculative.

### ADR-2 — `CompilationResult` is language-neutral; engine errors and compile errors are separate fields
**Decision:** In `Engines/`:
```csharp
public enum EngineError { None, ProjectNotFound, MultipleProjectsFound, ProjectFileUnreadable, TargetFrameworkNotFound, BuildOutputNotFound }
public enum CompilationDiagnosticSeverity { Warning, Error }
public class CompilationDiagnostic { string Id; string Message; CompilationDiagnosticSeverity Severity; string FilePath; int? Line; int? Column; }
public class CompilationResult
{
	EngineError Error; string Message; List<CompilationDiagnostic> Diagnostics = [];
	byte[] AssemblyImage = []; string BuildOutputDirectory;
	bool HasErrors { get { return Diagnostics.Any(d => d.Severity == CompilationDiagnosticSeverity.Error); } }
}
```
- `Error != None` means the engine could not compile; `Message` says why.
- `Error == None` with `HasErrors` means the code is wrong.
- `Line` and `Column` are 1-based and nullable, since a diagnostic can have no source location
  (e.g. a bad reference).
**Context:** Engine epic ADR-G3. `LanguageEngineResult` sets the house shape (`Error` + `Message` +
payload). A plain `Warning`/`Error` severity keeps Roslyn's `DiagnosticSeverity` out of the seam,
and dropping `Hidden`/`Info` (ADR-6) leaves two values.
**Alternatives rejected:**
- An `IsCompiled` bool alongside `HasErrors`: three flags for two facts.
- Roslyn `Diagnostic` in the result: ties the seam to C#.
- Reusing `LanguageEngineError`: that enum is about resolution, a different concern.

### ADR-3 — Project discovery: nearest `.csproj`, `TargetFramework` from the file, newest configuration wins
**Decision:** `FindProjectBuild.Find(sourceFilePath)` works as follows:
- **Find the project.** Walk up from the file's folder. The first folder with exactly one `*.csproj`
  is the project. Two or more → `MultipleProjectsFound`. Reaching the root → `ProjectNotFound`.
- **Read the properties.** Read the file through `IFileSystemTools.ReadAllText` and parse it with
  `System.Xml.Linq`. Malformed XML → `ProjectFileUnreadable`. Take the first `TargetFramework`
  element (none → `TargetFrameworkNotFound`) and `AssemblyName` (default: the project file name
  without extension).
- **Choose the configuration.** Among `bin/*/`, keep the folders where
  `bin/<config>/<tfm>/<AssemblyName>.dll` exists, and pick the one whose DLL has the latest
  `LastWriteTimeUtc`. None → `BuildOutputNotFound`.
- **Return** `ProjectBuild { ProjectFilePath, ProjectDirectory, AssemblyName, TargetFramework,
  OutputDirectory, IntermediateDirectory = obj/<config>/<tfm> }`.
**Context:** A stale `net10.0/` sits beside `net11.0/` today, so the TFM must come from the project.
"Newest wins" picks whatever the developer built last, Debug or Release, with no configuration.
`XmlException` from a malformed file is a predictable outcome, so it is caught and returned as a value
(`errors-and-logging.md`).
**Alternatives rejected:**
- Hard-coded `Debug`: wrong after a Release build.
- MSBuild evaluation (`Microsoft.Build.Locator`): the per-call MSBuild cost the README rules out.
- Any `bin/**/*.dll`: picks the stale TFM.
- Reading `project.assets.json`: more parsing for no gain given ADR-4.

### ADR-4 — References: build output (managed only) plus the runtime's framework assemblies, build output wins
**Decision:** `GatherCompilationReferences.Gather(outputDirectory)` builds the reference list in
three steps:
1. Take every `.dll` directly in `outputDirectory` that is a managed assembly (checked by
   `IInspectAssemblyFile`).
2. Add the trusted platform assemblies (TPA) whose path is under the runtime directory (read by
   `IReadTrustedPlatformAssemblies`), leaving out any whose file name already came from step 1
   (case-insensitive).
3. Return the paths with the build output first.

The two readers are `[ExcludeFromCodeCoverage]` wrappers over
`AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")` / `RuntimeEnvironment.GetRuntimeDirectory()` and
`System.Reflection.PortableExecutable.PEReader.HasMetadata`. All the filtering lives in the tested class.
**Context:**
- **Why TPA under the runtime directory.** TPA also lists the CLI's *own* dependencies (Roslyn,
  FatCat.Toolkit, Serilog), and these must not collide with the build output's versions.
  Restricting to the runtime directory keeps only `Microsoft.NETCore.App`.
- **Why the build output wins.** It sometimes carries a newer package copy of a framework assembly.
  Keeping it, and dropping the framework file of the same name, avoids duplicate-identity errors.
- **Why the managed check.** Build output can contain native DLLs, and referencing one fails the
  compile (CS0009).
- **Why the runtime and not the reference pack.** Item 2 runs the tests on this same runtime, so
  compiling against it matches what will execute.
**Alternatives rejected:**
- The reference pack (`packs/Microsoft.NETCore.App.Ref/<ver>/ref/<tfm>`): stricter, but it means
  locating the dotnet root and picking a version, and item 2 executes on the CLI runtime anyway.
  Revisit if a target TFM ever differs from the CLI's (Open Questions).
- All TPA entries: version collisions with the CLI's own dependencies.

### ADR-5 — Sources: submitted files plus each distinct project's global usings
**Decision:** `GatherCompilationSources.Gather(productionPath, testsPath, productionBuild, testBuild)`
returns `SourceFile { FilePath, Text }` in this order:
1. the production file;
2. the test file;
3. for each **distinct** project (by `ProjectFilePath`; the test project first),
   `<ProjectDirectory>/GlobalUsings.cs` if it exists, then
   `<IntermediateDirectory>/<project file name without extension>.GlobalUsings.g.cs` if it exists.

Text is read with `IFileSystemTools.ReadAllText`.
**Context:** House test files rely on `GlobalUsings.cs` (testing.md) and every project has
`ImplicitUsings` enabled. The generated file is named after the project file (verified), and it
exists whenever the build output does. Including the production project's usings keeps a production
file compiling even if it is ever under a different SDK (e.g. Web) whose implicit usings differ.
**Alternatives rejected:**
- Synthesizing the `ImplicitUsings` list in code: duplicates the SDK and drifts per SDK type.
- Scanning every project file for `global using`: reads the whole project, defeating the overlay.

### ADR-6 — Roslyn compile: library, nullable disabled, default language version, overlay noise suppressed
**Decision:** `CompileInMemory.Compile(assemblyName, sources, referencePaths)` works as follows:
- **Parse** each source with `CSharpParseOptions(LanguageVersion.Default)` and its file path.
- **Reference** each path through `MetadataReference.CreateFromFile`.
- **Build a `CSharpCompilation`** with `OutputKind.DynamicallyLinkedLibrary`,
  `NullableContextOptions.Disable` and `OptimizationLevel.Debug`.
- **Suppress** with `SpecificDiagnosticOptions`: `CS0436` (source type conflicts with an imported
  type, the shadowing in ADR-G2), `CS1701`/`CS1702` (assembly unification, which the SDK also
  suppresses), and the duplicate-using ids. Pin the duplicate-using ids by test: expected `CS0105`
  and `CS8933`.
- **Emit** to a `MemoryStream`.
- **Map diagnostics.** Keep only `Warning` and `Error`, with location → file path and 1-based
  line/column from `GetMappedLineSpan()`.
- **Return** `CompilationResult` with `Error = None`, the diagnostics, and `AssemblyImage` =
  the stream bytes only when emit succeeded (otherwise `[]`).
**Context:**
- **Default language version.** House csprojs use `<LangVersion>default</LangVersion>` and disable
  nullable. `LanguageVersion.Default` on the 5.12 compiler matches the SDK.
- **Debug optimization.** It keeps item 2's stack traces and the mutation gate's line mapping
  faithful.
- **Why suppress.** The suppressed ids are artifacts of the overlay, not defects in the submitted
  code. Reporting them would make the Compile gate block every unit.
**Alternatives rejected:**
- Filtering suppressed ids after compiling: suppression at the compilation level also stops
  warnings-as-errors promotion.
- Release optimization: line mapping degrades.
- Reporting `Info`/`Hidden`: noise (unused-using hints) for the gate.

### ADR-7 — `CSharpLanguageEngine` orchestrates; the assembly is named `<test AssemblyName>.Verify`
**Decision:** `CSharpLanguageEngine(IFindProjectBuild, IGatherCompilationSources, IGatherCompilationReferences, ICompileInMemory)`.
`Compile`:
1. Find the test build. On error, return a `CompilationResult` carrying its `Error`/`Message`.
2. Find the production build. On error, the same.
3. Gather sources and references (from the **test** build's `OutputDirectory`).
4. Compile as `$"{testBuild.AssemblyName}.Verify"`.
5. Set `BuildOutputDirectory = testBuild.OutputDirectory` on the result.
**Context:** One orchestration point, where the seam is (ADR-G1). It stays small (guard, guard,
gather, compile). The fixed name differs from the stale test DLL's name, so both load side by side
in item 2. Item 2 loads it into a collectible context, so repeated compiles (mutation) can reuse the
name in fresh contexts.
**Alternatives rejected:**
- A separate `CompileUnit` orchestrator class behind the engine: an extra layer with no second user.
- A GUID-suffixed name: nondeterministic and untestable, and the context, not the name, provides
  isolation.

### ADR-8 — Proof of the real overlay is a manual timed smoke run, not a committed test
**Decision:** Phase 5 ends with a **file-based app** (`dotnet run smoke.cs` with a
`#:project ../CodeWorker.Cli/CodeWorker.Cli.csproj` directive) in the session scratchpad, **not
committed**. It:
- initializes `SystemScope` the way `Program.Main` does;
- resolves `IResolveLanguageEngine` and the C# engine;
- compiles a real unit (`CodeWorker.Cli/Commands/Verify/Engines/ResolveLanguageEngine.cs` +
  `CodeWorker.Cli.Tests/Commands/Verify/Engines/ResolveLanguageEngineTests.cs`) after a
  `dotnet build`;
- prints the diagnostics count, image size and elapsed time.

The phase report records the result.
**Context:**
- **Why not a committed test.** Unit tests in this repo are one class per class under test with
  fakes (`testing.md`). A committed test that depends on the repo's live build layout would be a
  fragile, misnamed integration test.
- **Why this measurement.** A fresh process is the honest cold-Roslyn measurement the epic plan asks
  for, and a test-host run would hide JIT warm-up.
- **Where the committed proof lands.** The durable proof arrives with the Compile gate's E2E through
  the published CLI.
**Alternatives rejected:**
- A committed `…IntegrationTests` class in `CodeWorker.Cli.Tests`: breaks the 1:1 naming rule and
  couples the suite to the build output.
- A temporary `verify` caller: ADR-G5.

## Assumptions

- **Nullable disabled; `ImplicitUsings` enabled.** `System.Xml.Linq`, `System.Reflection.PortableExecutable`,
  `System.Runtime.InteropServices` (for `RuntimeEnvironment`), `System.Diagnostics.CodeAnalysis` and
  the `Microsoft.CodeAnalysis*` namespaces need explicit `using`s.
- **`Microsoft.CodeAnalysis.CSharp` resolves to ≥ 5.12.0.** The csproj uses `Version="*"` (house
  style for packages). Phase 1 confirms the restored version and the `LanguageVersion.Default`
  mapping.
- **The test project references the production project**, so the production DLL is in the test
  project's build output (true for every house test project).
- **Strong naming.** The CLI is signed and the Roslyn assemblies are strong-named, so the
  reference is fine. The emitted overlay is unsigned.
- **Task branch** `in-process-compilation` exists (PR #22). The orchestrator works on the current
  branch and stops if it is `main`.

## Open Questions

None blocking. Flag for the human reviewer:

- **Cold-start cost.** The smoke run measures it. If Roslyn's first compile costs most of the
  10-second budget, the answer belongs to *Publish + install* (ReadyToRun) or a new resident-process
  epic. Not here.
- **Runtime vs reference pack** (ADR-4). If a verified project's TFM ever differs from the CLI's
  runtime, compiling against runtime assemblies may accept APIs the target lacks. Switch to the
  reference pack then.
- **Publish must stay framework-dependent and multi-file.** Single-file or trimmed publish empties
  the TPA list and breaks Roslyn. *Publish + install* must respect this.
- **Helpers in the stale test DLL.** A test file using a helper that itself references the
  *changed* production type sees two different types (stale vs overlay) and fails to compile. This
  is rare under one-class units of work (Engine epic ADR-G2). It surfaces as a compile diagnostic,
  not silently.
- **Preprocessor symbols** (`DEBUG`, `NET11_0`, …) are not defined in the overlay compile. House code
  does not use `#if`. Add them if a gate ever needs them.
