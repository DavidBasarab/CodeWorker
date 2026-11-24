# Phase 4 — In-Memory Roslyn Compiler

- **Work item:** in-process-compilation (see `tasks/todo/engine/in-process-compilation/00-overview.md`)
- **Depends on:** Phase 1 (`01-compilation-contract.md`)
- **Depended on by:** Phase 5
- **Wave:** 2
- **Parallel with:** Phases 2 and 3. No dependency among them, disjoint write sets, no shared hotspot.
- **Risk:** **Medium** — first Roslyn use in the repo. The diagnostic mapping and the suppression
  list are exactly what the Compile gate will read.

## Touches

- Creates:
  - `CodeWorker.Cli/Commands/Verify/Engines/CSharp/CompileInMemory.cs`
  - `CodeWorker.Cli.Tests/Commands/Verify/Engines/CSharp/CompileInMemoryTests.cs`
- Modifies: —
- Exclusive runtime resources: none

You run in your own worktree while Phases 2 and 3 add other files under `Engines/CSharp/`. Create
**only** the files above.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Then read `tasks/todo/engine/in-process-compilation/00-overview.md`, ADR-2 and ADR-6, and
`tasks/todo/engine/00-epic-plan.md`, ADR-G2 (why the overlay shadows referenced types).

Current state you will find (Phase 1 committed):

- `CodeWorker.Cli.csproj` references `Microsoft.CodeAnalysis.CSharp` (≥ 5.12.0, matching SDK
  11.0.100's compiler).
- `Engines/CompilationResult.cs`: `Error`, `Message`, `Diagnostics`, `AssemblyImage` (default
  `[]`), `BuildOutputDirectory`, computed `HasErrors`.
- `Engines/CompilationDiagnostic.cs`: `Id`, `Message`, `Severity`, `FilePath`, `Line` (`int?`),
  `Column` (`int?`).
- `Engines/CompilationDiagnosticSeverity.cs`: `{ Warning, Error }`.
- `Engines/EngineError.cs`: `None` …
- `Engines/CSharp/SourceFile.cs`: `FilePath`, `Text`.

This class does **no** file reading of sources; it receives source text. It **does** read
reference assemblies from the given paths via `MetadataReference.CreateFromFile`. That is Roslyn's
own IO, acceptable here because the class is unit-testable with real paths (below).

## Design (build exactly this shape)

`CompileInMemory.cs`, namespace `FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp`:
```csharp
public interface ICompileInMemory
{
	CompilationResult Compile(string assemblyName, IReadOnlyList<SourceFile> sources, IReadOnlyList<string> referencePaths);
}

public class CompileInMemory : ICompileInMemory
```
Behavior (ADR-6):
1. **Parse.** `CSharpSyntaxTree.ParseText(source.Text, parseOptions, path: source.FilePath)` with
   `new CSharpParseOptions(LanguageVersion.Default)`.
2. **References.** `referencePaths.Select(path => MetadataReference.CreateFromFile(path))`.
3. **Options.**
   `new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable, optimizationLevel: OptimizationLevel.Debug)`,
   then `.WithSpecificDiagnosticOptions(...)` mapping each suppressed id to
   `ReportDiagnostic.Suppress`. Hold the suppressed ids as a named static collection, e.g.
   `OverlayDiagnostics`, so the reason is in the name, not a comment:
   - `CS0436` — a source type conflicts with an imported type (the overlay shadowing).
   - `CS1701`, `CS1702` — assembly-version unification.
   - **Duplicate global usings across the merged usings files.** Expected `CS0105` and `CS8933`.
     **Pin the real ids by the test below**: write the test, observe which id(s) Roslyn actually
     reports, suppress exactly those, and record them in the deviation log if they differ.
4. **Emit.** `CSharpCompilation.Create(assemblyName, trees, references, options)`, then
   `Emit(memoryStream)`.
5. **Map diagnostics.**
   - Keep only `DiagnosticSeverity.Warning` and `Error`, mapped to `CompilationDiagnosticSeverity`
     with a switch expression whose discard arm throws `ArgumentOutOfRangeException`.
   - `Id = diagnostic.Id`, `Message = diagnostic.GetMessage()`.
   - For in-source locations: `FilePath`, `Line = span.StartLinePosition.Line + 1`,
     `Column = span.StartLinePosition.Character + 1` from `diagnostic.Location.GetMappedLineSpan()`.
   - Otherwise `FilePath = null`, `Line`/`Column` null.
6. **Return** `CompilationResult { Error = EngineError.None, Diagnostics = mapped }`, with
   `AssemblyImage = memoryStream.ToArray()` **only when** `emitResult.Success`.

Keep `Compile` short. Extract parsing, options and mapping into named private methods.

## Steps (TDD — tests first, red before green)

`CompileInMemoryTests` uses **real Roslyn on tiny in-memory sources**. There are no fakes, since the
class has no dependencies.

**Framework references.** Add a private helper `FrameworkReferences()` that returns the test
process's trusted platform assemblies under `RuntimeEnvironment.GetRuntimeDirectory()`. Use it as
the reference list for every test. This is test setup, not production code.

**Sources.** Generate file paths with `Faker.Create<string>()` + `.cs`. Pin source text where
behavior depends on it. One assertion each:

- `ReturnNoErrorEngineResult` — `Error` is `EngineError.None` (clean source).
- `ReportNoDiagnosticsForCleanSource`
- `EmitAnAssemblyImageForCleanSource` — image non-empty.
- `ReportAnUndefinedNameAsAnError` — `Id` is `CS0103`.
- `ReportTheErrorSeverity`
- `ReportTheErrorsFilePath`
- `ReportTheErrorsOneBasedLine` — put the error on a known line.
- `ReportTheErrorsOneBasedColumn`
- `ReportTheErrorMessage` — message not empty.
- `EmitNoAssemblyImageWhenThereAreErrors`
- `ReportAWarningWithWarningSeverity` — e.g. `CS0168` (declared, never used).
- `NotReportHiddenOrInfoDiagnostics` — e.g. an unnecessary `using` (hidden CS8019) yields no
  diagnostics.
- `NotReportDuplicateGlobalUsingsAcrossSources` — two sources each with `global using System;` (and
  one also with a regular `using System;`) produce no diagnostics. **This test pins the
  duplicate-using ids** (see Design step 3).
- `NotReportASourceTypeShadowingAReferencedType` — declare a source class with the namespace and
  name of a real type in the CLI assembly (e.g. `FatCat.CodeWorker.Cli.Time.SystemClock`), add
  `typeof(SystemClock).Assembly.Location` to the references, and use the type from another source
  class. No CS0436 is reported.
- `CompileWithNullableDisabled` — `string? value = null;` in a source yields the nullable-context
  warning (CS8632), proving nullable is off. Assert on that id.
- `CompileFileScopedNamespacesAndPrimaryConstructors` — house-style syntax compiles cleanly (proves
  `LanguageVersion.Default` is current).

Red first, then implement to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (including the existing E2E suite)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; correct type-role suffixes; no banned
      patterns (no comments explaining code; the suppression list's reason is in its name)
- [ ] The suppressed duplicate-using ids are the ones the test observed (recorded in the report)
- [ ] Only the files in `## Touches` were created (wave-2 disjointness)
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the isolated worktree branch the orchestrator gave you, message referencing this file; no push

Suggested commit message:

```
in-process-compilation phase 4: in-memory Roslyn compiler (tasks/todo/engine/in-process-compilation/04-roslyn-compiler.md)
```

## Rollback Procedure

- Revert phase 5 first (it injects `ICompileInMemory`), then `git revert <this phase's commit>`. No
  manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); the final suppressed-id list and the
ids observed; deviation log (every departure from this plan and why — an empty log is a claim, not a
default); open questions/risks for the reviewer.

## Hand-off

- **`ICompileInMemory.Compile(assemblyName, sources, referencePaths) → CompilationResult`.**
  - Always returns `Error = None`.
  - Diagnostics are Warning/Error only, with 1-based locations.
  - `AssemblyImage` is filled only on a successful emit.
  - Overlay-caused ids are suppressed.
- Phase 5 sets `BuildOutputDirectory` on the returned result; this class leaves it `null`.
