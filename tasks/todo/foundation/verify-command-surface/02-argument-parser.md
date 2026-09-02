# Phase 2 — Verify Argument Model + Parser

- **Work item:** verify-command-surface (see
  `tasks/todo/foundation/verify-command-surface/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 3 (`03-wire-verify-command.md`)
- **Risk:** **Low** — a self-contained value type + a pure parser with one injected abstraction
  (`IFileSystemTools`, faked in tests). Not wired into the command yet, so it changes no runtime
  behavior on its own. No auth/network/data surface.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Pay special attention to `errors-and-logging.md` (**predictable failures return an enum,
never an exception**), `types-and-di.md` (plain classes, nullable disabled, `var`, constructor
injection, collection expressions), and `testing.md` (verb-first one-assertion tests, faked
`IFileSystemTools`, `Faker.Create<T>`).

This phase builds the **usage-validation authority** for `verify`: it turns the raw `args` into a
`VerifyArgumentsResult` that is either valid (three resolved paths) or a tagged usage error. It does
**not** read or parse the intent file's *contents* (that is item 2 of Foundation) and does not touch
`VerifyCommand` (that is Phase 3).

Current state you will find:

- Phase 1 delivered the command seam; `VerifyCommand` is a skeleton that ignores its args. This
  phase adds new files only — it does not modify `VerifyCommand`.
- `IFileSystemTools` (namespace `FatCat.Toolkit`) is injectable and auto-registered (overview
  ADR-4). Its `bool FileExists(string path)` is **synchronous** — verified against main-project call
  sites (`ClaudeRunner`, `RunSingleTaskCommand`, `CleanPendingArtifacts`). Fake it in tests with
  `A.Fake<IFileSystemTools>()` and `A.CallTo(() => fileSystemTools.FileExists(path)).Returns(true)`.
- House `*Result` style (`CodeWorker/Commands/Run/RepositoryValidationResult.cs`): a plain class
  with `{ get; set; }` auto-properties. Match it.
- The parser receives the **full** args array including the leading `"verify"` verb. Flags are found
  by scanning for `--intent` / `--production` / `--tests`, so the leading verb is naturally ignored
  (it is not a flag). Do not special-case or strip it.

## Design (build exactly this shape)

All three files under `CodeWorker.Cli/Commands/Verify/`, namespace
`FatCat.CodeWorker.Cli.Commands.Verify`.

**`VerifyUsageError.cs`** — the distinct, independently assertable failure tags (ADR-G2 / ADR-G4):

```csharp
namespace FatCat.CodeWorker.Cli.Commands.Verify;

public enum VerifyUsageError
{
	None,
	NoArguments,
	MissingIntentFlag,
	MissingProductionFlag,
	MissingTestsFlag,
	IntentFileNotFound,
	ProductionFileNotFound,
	TestsFileNotFound,
}
```

**`VerifyArgumentsResult.cs`** — the parsed-arguments value the rest of the spine consumes. Plain
class, nullable disabled (plain `string`, no `?`):

```csharp
namespace FatCat.CodeWorker.Cli.Commands.Verify;

public class VerifyArgumentsResult
{
	public bool IsValid { get; set; }

	public VerifyUsageError Error { get; set; }

	public string Message { get; set; }

	public string IntentPath { get; set; }

	public string ProductionPath { get; set; }

	public string TestsPath { get; set; }
}
```

**`ParseVerifyArguments.cs`** — interface + single impl in one file (named after the class):

```csharp
using FatCat.Toolkit;

namespace FatCat.CodeWorker.Cli.Commands.Verify;

public interface IParseVerifyArguments
{
	VerifyArgumentsResult Parse(string[] args);
}

public class ParseVerifyArguments(IFileSystemTools fileSystemTools) : IParseVerifyArguments
{
	private const string IntentFlag = "--intent";
	private const string ProductionFlag = "--production";
	private const string TestsFlag = "--tests";
	private const string UsageLine =
		"Usage: verify --intent <intent.json> --production <Foo.cs> --tests <FooTests.cs>";

	public VerifyArgumentsResult Parse(string[] args)
	{
		// (1) no flags at all -> NoArguments
		// (2) each flag present with a value -> Missing*Flag otherwise
		// (3) each named file exists (IFileSystemTools.FileExists) -> *FileNotFound otherwise
		// (4) otherwise valid, carrying the three resolved paths
	}
}
```

**Parsing rules (implement as short private helpers, one thing each):**

- A flag "has a value" when the token equals the flag (case-sensitive `--intent`) **and** a
  following token exists that is not itself another flag (does not start with `--`). Return that
  following token as the value. `TryGetFlagValue(args, flag, out value)`.
- **Precedence (return the first problem, one error at a time — deterministic and independently
  testable):**
  1. None of the three flags present at all → `NoArguments`, `Message = UsageLine`.
  2. `--intent` missing/without value → `MissingIntentFlag` (message names the flag + `UsageLine`).
  3. `--production` missing/without value → `MissingProductionFlag`.
  4. `--tests` missing/without value → `MissingTestsFlag`.
  5. `!FileExists(intentPath)` → `IntentFileNotFound` (message includes the path).
  6. `!FileExists(productionPath)` → `ProductionFileNotFound`.
  7. `!FileExists(testsPath)` → `TestsFileNotFound`.
  8. Otherwise → `IsValid = true`, `Error = None`, the three paths populated, `Message = null`.
- Every failure sets `IsValid = false` and a non-`None` `Error`. **Never throw** for any of these —
  they are predictable outcomes (`errors-and-logging.md`, ADR-G2).
- Messages are human-readable and specific, e.g.
  `$"Missing required flag {IntentFlag}. {UsageLine}"`,
  `$"Intent file not found: {intentPath}. {UsageLine}"`. Use string interpolation only.

Keep `Parse` short by extracting `TryGetFlagValue` and a `Failure(error, message)` local/helper that
builds a failed `VerifyArgumentsResult`. Do not use a mutable-state field pattern here — simple
locals suffice.

## Steps (TDD — tests first, red before green)

Tests in `CodeWorker.Cli.Tests/Commands/Verify/ParseVerifyArgumentsTests.cs`, namespace
`Testing.FatCat.CodeWorker.Cli.Commands.Verify`. Constructor: `fileSystemTools =
A.Fake<IFileSystemTools>()`; **default all files to existing** —
`A.CallTo(() => fileSystemTools.FileExists(A<string>._)).Returns(true)` — then override per test for
the not-found cases. Construct `parseVerifyArguments = new ParseVerifyArguments(fileSystemTools)`.
Build a well-formed args array in a helper, e.g.
`["verify", "--intent", "intent.json", "--production", "Foo.cs", "--tests", "FooTests.cs"]`, and vary
one thing per test. One assertion each:

1. `ReturnValidWhenAllFlagsPresentAndFilesExist` — `result.IsValid.Should().BeTrue()`.
2. `ReturnTheIntentPathWhenValid` — `result.IntentPath.Should().Be("intent.json")`.
3. `ReturnTheProductionPathWhenValid` — `.ProductionPath.Should().Be("Foo.cs")`.
4. `ReturnTheTestsPathWhenValid` — `.TestsPath.Should().Be("FooTests.cs")`.
5. `ReturnNoneErrorWhenValid` — `result.Error.Should().Be(VerifyUsageError.None)`.
6. `ReturnNoArgumentsWhenNoFlagsPresent` — `Parse(["verify"])` → `.Error.Be(NoArguments)`.
7. `ReturnNoArgumentsWhenArgsEmpty` — `Parse([])` → `.Error.Be(NoArguments)`.
8. `ReturnMissingIntentFlagWhenIntentAbsent` — args without `--intent` → `.Error.Be(MissingIntentFlag)`.
9. `ReturnMissingIntentFlagWhenIntentHasNoValue` — `["verify", "--intent", "--production", "Foo.cs", "--tests", "FooTests.cs"]`
   (intent flag followed by another flag) → `MissingIntentFlag`.
10. `ReturnMissingProductionFlagWhenProductionAbsent` → `MissingProductionFlag`.
11. `ReturnMissingTestsFlagWhenTestsAbsent` → `MissingTestsFlag`.
12. `ReturnIntentFileNotFoundWhenIntentMissing` — `A.CallTo(() => fileSystemTools.FileExists("intent.json")).Returns(false)`
    → `.Error.Be(IntentFileNotFound)`.
13. `ReturnProductionFileNotFoundWhenProductionMissing` — `FileExists("Foo.cs")` false → `ProductionFileNotFound`.
14. `ReturnTestsFileNotFoundWhenTestsMissing` — `FileExists("FooTests.cs")` false → `TestsFileNotFound`.
15. `ReturnInvalidWhenIntentFileMissing` — the not-found case also sets `IsValid.Should().BeFalse()`.
16. `PopulateAMessageWhenInvalid` — a missing-flag case → `result.Message.Should().NotBeNullOrEmpty()`.
17. `CheckIntentFlagPrecedesFileChecks` — when `--intent` is absent **and** files would not exist,
    the flag error wins (`MissingIntentFlag`, proving precedence order).

Then **implement** `VerifyUsageError`, `VerifyArgumentsResult`, and `ParseVerifyArguments` to green.

No smoke check needed — this phase adds no runtime path (the parser is exercised end-to-end in
Phase 3).

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln` — all tests pass
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespaces match folder paths exactly; one class per file (interface + single impl in
      `ParseVerifyArguments.cs`); `*Result` suffix on the value type; enum for the predictable
      failure set (no exceptions thrown for any usage error); string interpolation only; no banned
      patterns
- [ ] Review loop until all three pass clean, in order, restarting from the top after any fix:
      `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current branch (`CliTester`), message referencing this file; **no push**

Suggested commit message:

```
verify-command-surface phase 2: verify argument model + parser (tasks/todo/foundation/verify-command-surface/02-argument-parser.md)
```

## Rollback Procedure

- If Phase 3 exists, revert it first (it injects `IParseVerifyArguments`). Then
  `git revert <this commit>`.
- No data/config/feature-flag steps. This phase adds files only; reverting removes them.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (an empty log is a
claim, not a default) — in particular note any adaptation to the actual `IFileSystemTools.FileExists`
signature if it differs from the assumed `bool FileExists(string)`. Open questions/risks for the
reviewer.

## Hand-off

- **Types this phase exposes to Phase 3 (and later Foundation items):**
  - `FatCat.CodeWorker.Cli.Commands.Verify.VerifyArgumentsResult` — `{ IsValid, Error, Message,
    IntentPath, ProductionPath, TestsPath }`. This is the parsed-arguments value the pipeline (items
    2–4) consumes.
  - `FatCat.CodeWorker.Cli.Commands.Verify.VerifyUsageError` — the failure tag set.
  - `FatCat.CodeWorker.Cli.Commands.Verify.IParseVerifyArguments` / `ParseVerifyArguments` —
    `VerifyArgumentsResult Parse(string[] args)`; never throws for a predictable input.
- **Behavior notes for later phases:** the parser returns exactly one error at a time in the fixed
  precedence flags-before-files; a valid result guarantees all three files existed **at parse time**.
  It validates the intent file *path only* — item 2 (Intent contract model) parses its contents.
