# Phase 1 — Intent Model + Contract Validator

- **Work item:** intent-contract-model (see
  `tasks/todo/foundation/intent-contract-model/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 2 (`02-intent-file-parser.md`)
- **Risk:** **Low** — new, isolated value types and a pure, synchronous validator with no injected
  dependencies. Not wired into any runtime path yet. No auth/network/data surface.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` (especially "The Intent Contract"), `CLAUDE.md`, and **all**
`.claude/rules/csharp/*.md` first — mandatory. Pay special attention to `errors-and-logging.md`
(predictable failures return an enum, never an exception), `types-and-di.md` (plain classes, records
banned, nullable disabled, collection expressions, `var`), `toolchain.md` (no expression-bodied
members anywhere, including tests), and `testing.md` (verb-first, one assertion per test).

This phase defines the **intent contract model** and the **validator** that decides whether a
deserialized contract is complete and checkable. It does **not** read files or parse JSON (Phase 2)
and does not touch `VerifyCommand` (Phase 3).

Current state you will find:

- `CodeWorker.Cli/Commands/Verify/` holds item 1's work: `VerifyCommand.cs`,
  `ParseVerifyArguments.cs`, `VerifyArgumentsResult.cs`, `VerifyUsageError.cs`. **Do not modify
  them.** `VerifyArgumentsResult` (`{ IsValid, Error, Message, IntentPath, ProductionPath, TestsPath }`)
  is the shape to mirror for `IntentResult`.
- There is no `Commands/Verify/Intent/` folder yet — create it (source and test project).
- `ParseVerifyArguments.cs` shows the house pattern: interface + single implementation in one file
  named after the class, private `const` strings, short `private static` helpers, a `Failure(...)`
  helper that builds a failed result.

## Design (build exactly this shape)

All files under `CodeWorker.Cli/Commands/Verify/Intent/`, namespace
`FatCat.CodeWorker.Cli.Commands.Verify.Intent`. Block bodies only.

**`IntentBehavior.cs`**

```csharp
namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public class IntentBehavior
{
	public string Behavior { get; set; }

	public string Test { get; set; }
}
```

**`IntentContract.cs`** — the README's contract; JSON names `why`, `class`, `responsibility`,
`behaviors` map case-insensitively in Phase 2 (no attributes):

```csharp
namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public class IntentContract
{
	public string Why { get; set; }

	public string Class { get; set; }

	public string Responsibility { get; set; }

	public List<IntentBehavior> Behaviors { get; set; } = [];
}
```

**`IntentValidationError.cs`** — covers the whole intent stage (Phase 2 uses the first two):

```csharp
namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public enum IntentValidationError
{
	None,
	IntentFileUnreadable,
	MalformedIntentJson,
	MissingWhy,
	MissingClass,
	ClassDoesNotMatchProductionFile,
	MissingResponsibility,
	NoBehaviors,
	EmptyBehavior,
	NonObservableBehavior,
	InvalidTestName,
}
```

**`IntentResult.cs`**

```csharp
namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public class IntentResult
{
	public bool IsValid { get; set; }

	public IntentValidationError Error { get; set; }

	public string Message { get; set; }

	public IntentContract Intent { get; set; }
}
```

**`ValidateIntent.cs`** — interface + single impl, one file named after the class:

```csharp
using System.Text.RegularExpressions;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public interface IValidateIntent
{
	IntentResult Validate(IntentContract intent, string productionPath);
}

public class ValidateIntent : IValidateIntent
{
	// ObservableVerbs (case-insensitive set), NegationPrefixes ["Does not ", "Never "],
	// TestNamePattern ^[A-Za-z_][A-Za-z0-9_]*$
	public IntentResult Validate(IntentContract intent, string productionPath) { ... }
}
```

**Validation rules — return the first problem only, in this exact precedence:**

1. `string.IsNullOrWhiteSpace(intent.Why)` → `MissingWhy`.
2. `string.IsNullOrWhiteSpace(intent.Class)` → `MissingClass`.
3. `intent.Class.Trim()` is not ordinally equal to `Path.GetFileNameWithoutExtension(productionPath)`
   → `ClassDoesNotMatchProductionFile`.
4. `string.IsNullOrWhiteSpace(intent.Responsibility)` → `MissingResponsibility`.
5. `intent.Behaviors` is `null` or empty → `NoBehaviors`.
6. For each behavior **in order** (1-based `number`), check in this order and return on the first
   failure:
   a. the entry is `null` or `string.IsNullOrWhiteSpace(entry.Behavior)` → `EmptyBehavior`;
   b. not observable (below) → `NonObservableBehavior`;
   c. `entry.Test != null` and `entry.Test` does not match the test-name pattern → `InvalidTestName`
      (so `""` and `"   "` are invalid; `null` means omitted and is valid).
7. Otherwise → `IsValid = true`, `Error = None`, `Message = null`, `Intent = intent`.

Every failure: `IsValid = false`, the tag, a `Message`, and `Intent = intent` (so a later report can
show what was submitted). **Never throw** for any of these.

**Observable check (overview ADR-3):** take `entry.Behavior.Trim()`; if it starts with `"Does not "`
or `"Never "` (`StringComparison.OrdinalIgnoreCase`), remove that prefix and trim again; the first
whitespace-delimited word must be in the allow-list, compared case-insensitively:

```
Returns, Throws, Calls, Invokes, Logs, Writes, Reads, Saves, Loads, Creates, Adds, Removes,
Deletes, Sets, Updates, Sends, Raises, Records, Moves, Reports, Resolves, Executes, Emits, Skips, Stops
```

Hold it as `private static readonly HashSet<string> ObservableVerbs = new(StringComparer.OrdinalIgnoreCase) { ... }`
(a `HashSet` with a comparer cannot be a bare collection expression — the explicit type is the
documented exception in `types-and-di.md`). Hold the pattern as a `private static readonly Regex`.

**Messages (string interpolation only; tests assert on the tags, plus one message test per family):**

- `MissingWhy` — `Intent is missing "why" — the reason this code needs to exist.`
- `MissingClass` — `Intent is missing "class" — the production class this intent describes.`
- `ClassDoesNotMatchProductionFile` — `$"Intent class \"{intent.Class}\" does not match the production file {Path.GetFileName(productionPath)} — expected \"{expectedClass}\"."`
- `MissingResponsibility` — `Intent is missing "responsibility" — what this class is responsible for.`
- `NoBehaviors` — `Intent declares no behaviors — at least one observable behavior is required.`
- `EmptyBehavior` — `$"Intent behavior {number} is empty."`
- `NonObservableBehavior` — `$"Intent behavior {number} is not observable: \"{text}\". Start it with an outcome verb such as Returns, Throws, Calls, or Logs."`
- `InvalidTestName` — `$"Intent behavior {number} names test \"{test}\", which is not a valid test method name."`

Keep `Validate` short: extract `ValidateBehaviors(...)`, `IsObservable(string)`, `IsValidTestName(string)`,
and `Failure(IntentContract, IntentValidationError, string)` helpers. No mutable-state fields needed.

## Steps (TDD — tests first, red before green)

Tests in `CodeWorker.Cli.Tests/Commands/Verify/Intent/ValidateIntentTests.cs`, namespace
`Testing.FatCat.CodeWorker.Cli.Commands.Verify.Intent`. `validateIntent = new ValidateIntent()` in
the constructor. A helper builds a valid contract with every field the assertions depend on pinned:

```csharp
private const string ProductionPath = @"C:\Repo\Src\TokenLimitHeuristic.cs";

private static IntentContract ValidIntent()
{
	return new IntentContract
	{
		Why = "Tasks over the token limit must be re-queued.",
		Class = "TokenLimitHeuristic",
		Responsibility = "Classify a ProcessResult as TokenLimited.",
		Behaviors =
		[
			new IntentBehavior { Behavior = "Returns TokenLimited when the marker is present", Test = "ReturnTokenLimitedWhenMarkerPresent" },
		],
	};
}
```

Write these failing tests first (one assertion each), then implement to green:

1. `ReturnValidForACompleteIntent` — `IsValid` true.
2. `ReturnNoneErrorForACompleteIntent` — `Error` `None`.
3. `ReturnTheIntentWhenValid` — `Intent` is the same instance passed in (`BeSameAs`).
4. `ReturnMissingWhyWhenWhyIsBlank` — `Why = "  "` → `MissingWhy`.
5. `ReturnMissingClassWhenClassIsNull` → `MissingClass`.
6. `ReturnClassDoesNotMatchProductionFileWhenNamesDiffer` — `Class = "Other"` → tag.
7. `ReturnClassDoesNotMatchProductionFileWhenCaseDiffers` — `Class = "tokenLimitHeuristic"` → tag.
8. `AcceptAClassWithSurroundingWhitespace` — `Class = " TokenLimitHeuristic "` → valid.
9. `NameTheExpectedClassWhenClassDoesNotMatch` — message contains `"TokenLimitHeuristic"`.
10. `ReturnMissingResponsibilityWhenResponsibilityIsEmpty` → tag.
11. `ReturnNoBehaviorsWhenBehaviorsIsEmpty` → tag.
12. `ReturnNoBehaviorsWhenBehaviorsIsNull` → tag.
13. `ReturnEmptyBehaviorWhenABehaviorIsBlank` → tag.
14. `ReturnEmptyBehaviorWhenABehaviorEntryIsNull` → tag.
15. `ReturnNonObservableBehaviorForAnImplementationDetail` — `"Uses a switch on the outcome"` → tag.
16. `NameTheBehaviorNumberWhenNotObservable` — second behavior non-observable → message contains `"behavior 2"`.
17. `AcceptAnObservableVerbCaseInsensitively` — `"returns X when Y"` → valid.
18. `AcceptANegatedObservableBehavior` — `"Does not call the logger when Y"` → valid.
19. `AcceptANeverPrefixedObservableBehavior` — `"Never writes the file when Y"` → valid.
20. `ReturnNonObservableBehaviorWhenOnlyANegationPrefix` — `"Does not"` (nothing after) → `NonObservableBehavior`.
21. `AcceptABehaviorWithoutATest` — `Test = null` → valid.
22. `ReturnInvalidTestNameWhenTestHasSpaces` — `"returns x when y"` → tag.
23. `ReturnInvalidTestNameWhenTestIsEmpty` — `""` → tag.
24. `ReturnInvalidTestNameWhenTestStartsWithADigit` — `"1ReturnX"` → tag.
25. `CheckWhyBeforeClass` — `Why` blank **and** `Class` null → `MissingWhy` (precedence).
26. `CheckBehaviorTextBeforeTestName` — behavior non-observable **and** test invalid →
    `NonObservableBehavior` (precedence within a behavior).
27. `ReturnInvalidForAnyFailure` — e.g. `MissingWhy` case → `IsValid` false.
28. `KeepTheIntentOnFailure` — failure result's `Intent` is the submitted instance.

Implement the five files to green. No smoke check — nothing is wired yet.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln` — all tests pass
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespaces match folder paths exactly; one class per file (`IValidateIntent` lives in
      `ValidateIntent.cs`); `*Result` suffix on `IntentResult`; enum for the predictable failure set;
      no records; no expression-bodied members; string interpolation only; no banned patterns
- [ ] Review loop until all three pass clean, in order, restarting from the top after any fix:
      `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current branch (`intent-contract-model`), message referencing this
      file; **no push**

Suggested commit message:

```
intent-contract-model phase 1: intent model + contract validator (tasks/todo/foundation/intent-contract-model/01-intent-model-validation.md)
```

## Rollback Procedure

- Revert Phase 3, then Phase 2, if they exist (both depend on these types). Then
  `git revert <this commit>`.
- No data/config/feature-flag steps — this phase only adds files.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (every departure from
this plan and why — an empty log is a claim, not a default), in particular any change to the verb
list, the message texts, or the precedence; open questions/risks for the reviewer.

## Hand-off

- **Types this phase exposes (namespace `FatCat.CodeWorker.Cli.Commands.Verify.Intent`):**
  - `IntentContract { Why, Class, Responsibility, List<IntentBehavior> Behaviors }` and
    `IntentBehavior { Behavior, Test }` — Phase 2 deserializes into these.
  - `IntentValidationError` — Phase 2 returns `IntentFileUnreadable` / `MalformedIntentJson`.
  - `IntentResult { IsValid, Error, Message, Intent }` — the value Phases 2 and 3 return/consume and
    item 3's pipeline will read `Intent` from.
  - `IValidateIntent.Validate(IntentContract intent, string productionPath) → IntentResult` —
    synchronous; never throws for any contract content; returns exactly one error in fixed precedence.
- **Behavior notes:** `Intent` is populated on every result the validator returns (valid or not);
  `Test == null` means the link was omitted. The validator expects a non-null `intent` — Phase 2 maps a
  JSON `null` to `MalformedIntentJson` before calling it.
