# Work Item

`in-process-compilation`

# Specification

Give the C# language engine its first real operation: **compile a unit of work in memory with
Roslyn**, with no MSBuild and no `dotnet build` per call. A unit of work is one production file plus
its test file. The result is structured compiler diagnostics and, when there are no errors, the
emitted assembly bytes.

Grounded in `CodeWorker.Cli/README.md` ("The engine runs in-process — that's how it stays under 10
seconds") and the `epics.md` item text:

- **Compile in memory.** The changed class and its test are compiled with Roslyn "against the
  project's already-built reference assemblies. No `dotnet build`, no MSBuild per call."
- **Resolve references once from the existing build output.** The test project's `bin` output for
  its `TargetFramework` already contains the production DLL and every package. That output, plus the
  framework assemblies, is the reference set.
- **Overlay, not recompile** (Engine epic ADR-G2). The compilation contains only the submitted
  production file, the submitted test file, and the global usings of the projects they belong to.
  The submitted types shadow their stale copies in the referenced DLLs.
- **Diagnostics are returned structured.** Each diagnostic has an id, a message, a severity, a file,
  and a 1-based line and column. A compile error is **data**: the engine ran successfully and found
  errors. Whether it blocks is the Compile gate's call (next group).
- **Engine errors are values** (Engine epic ADR-G3; Foundation ADR-G2). These cover being unable to
  find the owning `.csproj`, finding several, a `.csproj` that cannot be read, no `TargetFramework`
  in it, or no build output. Each comes back as an `EngineError` value with a message, never an
  exception.
- **Behind the seam.** The operation is a new member of `ILanguageEngine`, implemented by
  `CSharpLanguageEngine`. The result types are language-neutral; Roslyn types stay inside
  `Engines/CSharp/` (Engine epic ADR-G1).

Nothing in `verify` calls it yet. The Compile gate (*Deterministic gates*) is the first caller
(Engine epic ADR-G5).

## Acceptance Criteria

- [ ] `ILanguageEngine` exposes `Task<CompilationResult> Compile(string productionPath, string testsPath)`;
      `CSharpLanguageEngine` implements it. The identity members (`Language`, `FileExtensions`) are
      unchanged.
- [ ] Given a production file and a test file in built projects, `Compile` finds each file's owning
      `.csproj` by walking up from the file's folder.
- [ ] The build output is `<project>/bin/<configuration>/<TargetFramework>/` containing
      `<AssemblyName>.dll`. When several configurations qualify, the one whose `<AssemblyName>.dll`
      was written most recently wins. `AssemblyName` defaults to the project file name.
- [ ] Each failure returns its own `EngineError` with a message naming the path, and `Compile`
      returns before compiling:
      - no `.csproj` up to the root → `ProjectNotFound`
      - two or more `.csproj` in one folder → `MultipleProjectsFound`
      - malformed `.csproj` XML → `ProjectFileUnreadable`
      - no `<TargetFramework>` → `TargetFrameworkNotFound`
      - no qualifying output → `BuildOutputNotFound`
- [ ] The source set is the production file, the test file, and for each distinct owning project
      its `GlobalUsings.cs` (if present) and its SDK-generated
      `obj/<configuration>/<tfm>/<ProjectFileName>.GlobalUsings.g.cs` (if present). So a test file
      using `Xunit`, `FakeItEasy`, `System.Linq`, … compiles without restating usings.
- [ ] The reference set is every **managed** `.dll` in the test project's build output, plus the
      running runtime's framework assemblies, taken from the trusted platform assemblies under the
      runtime directory. A framework assembly whose file name is already in the build output is
      dropped (the build output copy wins). Native DLLs are never referenced.
- [ ] A clean unit compiles with no error diagnostics and a non-empty assembly image.
- [ ] A unit with a compile error returns the error's id (e.g. `CS0103`), message, file path, line
      and column (1-based). The assembly image is empty, `HasErrors` is true, and `Error` is still
      `None`.
- [ ] Warnings are returned with `Warning` severity. Hidden and info diagnostics are not returned.
- [ ] Diagnostics the overlay itself causes are suppressed for this compilation: a source type
      shadowing a referenced type (CS0436), duplicate global usings across the merged usings files,
      and assembly-version unification (CS1701/CS1702).
- [ ] The emitted assembly is named `<test AssemblyName>.Verify`. `CompilationResult` carries the
      test project's build output directory, which the test runner (item 2) loads dependencies from.
- [ ] A timed smoke run (fresh process, cold Roslyn) compiles a real unit from this repository with
      no errors, and the elapsed time is recorded in the phase report.
- [ ] Discovery, source gathering and reference gathering are unit-tested against a faked
      `IFileSystemTools`. The Roslyn compiler is unit-tested with real Roslyn on in-memory sources.
      Zero warnings; `dotnet test CodeWorker.slnx` green.

## Out of Scope / Non-Goals

- **The Compile gate**, and any change to `VerifyCommand`, the pipeline or the verdict. That is the
  next group (Engine epic ADR-G5).
- **Running tests** and loading the assembly into a load context: item 2 (`in-process-test-runner`).
- **Mutation** (re-emitting changed syntax trees) and **scope-to-diff**: later groups.
- **Recompiling other changed classes** in the project. The overlay sees only the submitted files
  (Engine epic ADR-G2).
- `TargetFrameworks` (plural, multi-targeting), `Directory.Build.props`-supplied properties,
  `FrameworkReference`s beyond `Microsoft.NETCore.App` (ASP.NET, WindowsDesktop), and
  `InternalsVisibleTo`. These return or surface errors; none are solved here.
- Choosing the build configuration by config file: *Tool-level configuration* may add it later.
- Caching compilations between calls: Full, *Build/test caching*.

## Feature Context

- Epic plan: [`tasks/todo/engine/00-epic-plan.md`](../00-epic-plan.md), item 1 and ADR-G1..G5.
- The seam it extends: [`language-engine-seam`](../../../done/foundation/language-engine-seam/00-overview.md)
  (ADR-1, identity-only interface).
- Error-as-value pattern to mirror: `CodeWorker.Cli/Commands/Verify/Engines/LanguageEngineResult.cs`.
- Source of truth: [`CodeWorker.Cli/README.md`](../../../../CodeWorker.Cli/README.md), "The engine
  runs in-process".
- File system abstraction: `C:\Code\FatCat.Toolkit\src\ToolKit\FileSystemTools.cs` (`IFileSystemTools`).
