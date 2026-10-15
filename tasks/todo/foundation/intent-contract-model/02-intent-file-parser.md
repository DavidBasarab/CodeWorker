# Phase 2 — Intent File Parser

- **Work item:** intent-contract-model (see
  `tasks/todo/foundation/intent-contract-model/00-overview.md`)
- **Depends on:** Phase 1 (`01-intent-model-validation.md`)
- **Depended on by:** Phase 3 (`03-wire-intent-into-verify.md`)
- **Risk:** **Low** — one new class; file IO sits behind the faked `IFileSystemTools`. It is the one
  place IO/JSON exceptions become values, so the catch set must be exactly the predictable ones (a
  too-broad catch would swallow real faults). Not wired into `VerifyCommand` yet.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("The Intent Contract"), `CLAUDE.md`, and **all**
`.claude/rules/csharp/*.md` first — mandatory. Especially `errors-and-logging.md`: predictable
failures are returned values; do not catch-and-swallow; exceptions that are genuinely unexpected
bubble to the boundary (`Program.Main`).

This phase builds **`ParseIntent`**: it reads the intent file named by the already-validated verify
arguments, deserializes it with `System.Text.Json` into Phase 1's `IntentContract`, converts the
predictable read/JSON failures into `IntentResult` values, and delegates content validation to
Phase 1's `IValidateIntent`.

Current state you will find (Phase 1 is committed):

- `CodeWorker.Cli/Commands/Verify/Intent/` contains `IntentBehavior`, `IntentContract`
  (`Why`, `Class`, `Responsibility`, `List<IntentBehavior> Behaviors = []`), `IntentValidationError`
  (`None, IntentFileUnreadable, MalformedIntentJson, MissingWhy, …`), `IntentResult`
  (`IsValid, Error, Message, Intent`), and `ValidateIntent.cs` with
  `IValidateIntent.Validate(IntentContract intent, string productionPath) → IntentResult`.
- `CodeWorker.Cli/Commands/Verify/VerifyArgumentsResult.cs` — `{ IsValid, Error, Message, IntentPath, ProductionPath, TestsPath }`
  (namespace `FatCat.CodeWorker.Cli.Commands.Verify`). Item 1's parser has already proven the files
  exist when `IsValid` is true.
- `IFileSystemTools` (`using FatCat.Toolkit;`) exposes `Task<string> ReadAllText(string path)` and
  auto-registers via `SystemScope`. Fake with `A.Fake<IFileSystemTools>()`.
- House JSON pattern to copy — `CodeWorker/Commands/Run/LoadRepoSettings.cs`:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
...
var json = await fileSystemTools.ReadAllText(settingsPath);
return JsonSerializer.Deserialize<RepoSettings>(json, JsonOptions);
```

## Design (build exactly this shape)

**`CodeWorker.Cli/Commands/Verify/Intent/ParseIntent.cs`**, namespace
`FatCat.CodeWorker.Cli.Commands.Verify.Intent`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using FatCat.Toolkit;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public interface IParseIntent
{
	Task<IntentResult> Parse(VerifyArgumentsResult arguments);
}

public class ParseIntent(IFileSystemTools fileSystemTools, IValidateIntent validateIntent) : IParseIntent
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
	};

	public async Task<IntentResult> Parse(VerifyArgumentsResult arguments) { ... }
}
```

(`VerifyArgumentsResult` is in the parent namespace `FatCat.CodeWorker.Cli.Commands.Verify` — add the
`using` if the compiler requires it.)

**Flow — first problem wins:**

1. Read: `json = await fileSystemTools.ReadAllText(arguments.IntentPath)`. Catch **only**
   `IOException` and `UnauthorizedAccessException` (an exception filter:
   `catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)`) →
   `IntentFileUnreadable`, message
   `$"Intent file could not be read: {arguments.IntentPath}. {exception.Message}"`.
   This covers a locked file and a path that is a directory.
2. **Empty or missing content (amended 2026-10-14 after the first Phase 2 attempt failed):**
   FatCat's `IFileSystemTools.ReadAllText` does **not** throw for a missing file. It returns
   `string.Empty` (`C:\Code\FatCat.Toolkit\src\ToolKit\FileSystemTools.cs`:
   `FileDoesNotExist(path) ? string.Empty : ...`). So when
   `string.IsNullOrWhiteSpace(json)` is true, return `IntentFileUnreadable` with message
   `$"Intent file could not be read: {arguments.IntentPath}. The file is empty or missing."`.
   This covers a file deleted after item 1's existence check, as well as a genuinely empty file.
   **Do not** add a `fileSystemTools.FileExists` guard: item 1 already checks existence
   (`IntentFileNotFound`), a second check would be racy and would give two error codes for one
   condition, and code-review rejected it on the first attempt.
3. Deserialize: `JsonSerializer.Deserialize<IntentContract>(json, JsonOptions)`. Catch **only**
   `JsonException` → `MalformedIntentJson`, message
   `$"Intent file is not a valid intent JSON object: {arguments.IntentPath}. {exception.Message}"`.
   This covers syntax errors, a non-object root (`[]`, `"text"`, `42`), and an unknown property
   (rejected by `Disallow`). Keep `JsonOptions` exactly as designed above. **Do not** set
   `AllowDuplicateProperties = false`: duplicate properties are allowed and the last one wins (user
   decision 2026-10-14). Strict JSON stays, so comments and trailing commas are malformed (ADR-5).
4. A `null` contract (the file is literally `null`) → `MalformedIntentJson`, message
   `$"Intent file is not a valid intent JSON object: {arguments.IntentPath}. The file contains no intent object."`.
5. Otherwise → `return validateIntent.Validate(contract, arguments.ProductionPath);` (its result is
   returned as-is).

Failures built here set `IsValid = false`, the tag, the message, and `Intent = null` (nothing was
deserialized). Extract `ReadIntentJson`/`Deserialize` and a `Failure(error, message)` helper to keep
`Parse` short. Because two steps can fail before validation, use small private helpers that return
`IntentResult` (or `null` for "no failure") — or a mutable-state field pair scoped to one call per the
rules — whichever reads more clearly; **do not** throw to signal a failure between helpers.

## Steps (TDD — tests first, red before green)

Tests in `CodeWorker.Cli.Tests/Commands/Verify/Intent/ParseIntentTests.cs`, namespace
`Testing.FatCat.CodeWorker.Cli.Commands.Verify.Intent`.

Constructor setup:
- `fileSystemTools = A.Fake<IFileSystemTools>()`;
  `A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).ReturnsLazily(() => currentJson)` with a
  `private string currentJson` defaulting to the sample below.
- `validateIntent = A.Fake<IValidateIntent>()`; capture the contract it receives and return a sentinel:
  `A.CallTo(() => validateIntent.Validate(A<IntentContract>._, A<string>._)).ReturnsLazily((IntentContract intent, string _) => { capturedIntent = intent; return validatedResult; });`
  where `validatedResult = new IntentResult { IsValid = true, Error = IntentValidationError.None }`.
- `arguments = new VerifyArgumentsResult { IsValid = true, IntentPath = Faker.Create<string>(), ProductionPath = Faker.Create<string>(), TestsPath = Faker.Create<string>() }`
  (Phase 2 tests assert against `arguments.IntentPath` / `arguments.ProductionPath`, not literals).
- Do not add `using System.IO;` — `ImplicitUsings` already imports it.
- `parseIntent = new ParseIntent(fileSystemTools, validateIntent)`.

Sample JSON (the README example — the round-trip fixture), as a `private const string`:

```json
{
  "why": "Tasks that exceed the token limit must be re-queued as pending, not failed.",
  "class": "TokenLimitHeuristic",
  "responsibility": "Classify a ProcessResult as TokenLimited when the transcript shows the token-limit signature.",
  "behaviors": [
    { "behavior": "Returns TokenLimited when the transcript contains the limit marker", "test": "ReturnTokenLimitedWhenMarkerPresent" },
    { "behavior": "Returns null when no marker is present" }
  ]
}
```

Failing tests first (one assertion each):

1. `ReadTheIntentFileFromTheIntentPath` — `ReadAllText(arguments.IntentPath)` `MustHaveHappenedOnceExactly`.
2. `DeserializeTheWhy` — `capturedIntent.Why` equals the sample's why.
3. `DeserializeTheClass` — `capturedIntent.Class` is `"TokenLimitHeuristic"`.
4. `DeserializeTheResponsibility`.
5. `DeserializeEveryBehavior` — `capturedIntent.Behaviors` has count 2.
6. `DeserializeTheBehaviorText` — `Behaviors[0].Behavior` equals the first behavior text.
7. `DeserializeTheTestLink` — `Behaviors[0].Test` is `"ReturnTokenLimitedWhenMarkerPresent"`.
8. `LeaveTheTestNullWhenOmitted` — `Behaviors[1].Test` is `null`.
9. `MatchPropertyNamesCaseInsensitively` — JSON using `"Why"`, `"CLASS"`, … → `capturedIntent.Class` populated.
10. `ValidateAgainstTheProductionPath` — `Validate(A<IntentContract>._, arguments.ProductionPath)` `MustHaveHappenedOnceExactly`.
11. `ReturnTheValidationResult` — result `BeSameAs(validatedResult)`.
12. `ReturnIntentFileUnreadableWhenReadThrowsIOException` — `ReadAllText` `.Throws(new IOException("locked"))` → `Error` tag.
13. `ReturnIntentFileUnreadableWhenAccessIsDenied` — throws `UnauthorizedAccessException` → tag.
14. `ReturnIntentFileUnreadableWhenTheFileIsEmptyOrMissing` — `currentJson = ""` (what the real
    toolkit returns for a missing file) → `IntentFileUnreadable`. Do **not** fake a
    `FileNotFoundException`, because the toolkit never throws one.
15. `NameTheIntentPathWhenUnreadable` — message contains the intent path.
16. `ReturnMalformedIntentJsonForInvalidSyntax` — `currentJson = "{ not json"` → tag.
17. `ReturnIntentFileUnreadableForWhitespaceContent` — `"   \n"` → `IntentFileUnreadable`.
17a. `NotValidateWhenTheFileIsEmptyOrMissing` — `""` → `Validate` `MustNotHaveHappened`.
17b. `NotCheckFileExistence` — `fileSystemTools.FileExists(A<string>._)` `MustNotHaveHappened`
    (item 1 owns existence).
18. `ReturnMalformedIntentJsonForANullPayload` — `"null"` → tag.
19. `ReturnMalformedIntentJsonForAnArrayPayload` — `"[]"` → tag.
20. `ReturnMalformedIntentJsonForAnUnknownProperty` — sample with `"tests"` instead of `"test"` → tag.
21. `NotValidateWhenTheJsonIsMalformed` — `"{ not json"` → `Validate` `MustNotHaveHappened`.
22. `NotValidateWhenTheFileIsUnreadable` — IOException → `Validate` `MustNotHaveHappened`.
23. `ReturnInvalidWhenTheJsonIsMalformed` — `IsValid` false.
24. `LetUnexpectedExceptionsPropagate` — `ReadAllText` throws `InvalidOperationException` →
    `FluentActions.Awaiting(() => parseIntent.Parse(arguments)).Should().ThrowAsync<InvalidOperationException>()`
    (proves the catch set is narrow — no swallowing).

Implement `ParseIntent` to green. No smoke check — not wired until Phase 3.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln` — all tests pass
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespace matches folder; interface + single impl in `ParseIntent.cs`; constructor injection
      only; catch set is exactly `IOException`/`UnauthorizedAccessException`/`JsonException`; no
      `catch (Exception)` without a filter; no blocking on tasks; no banned patterns
- [ ] Review loop until all three pass clean, in order, restarting from the top after any fix:
      `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current branch (`intent-contract-model`), message referencing this
      file; **no push**

Suggested commit message:

```
intent-contract-model phase 2: intent file parser (tasks/todo/foundation/intent-contract-model/02-intent-file-parser.md)
```

## Rollback Procedure

- Revert Phase 3 first if it exists (it injects `IParseIntent`). Then `git revert <this commit>`.
- No data/config/feature-flag steps — this phase only adds files.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (an empty log is a
claim, not a default) — note especially how the read/deserialize helpers signal "no failure", and any
`System.Text.Json` behavior that differed from this plan (e.g. how `Disallow` or a non-object root
surfaces). Open questions/risks for the reviewer.

## Hand-off

- **Exposes (namespace `FatCat.CodeWorker.Cli.Commands.Verify.Intent`):**
  `IParseIntent.Parse(VerifyArgumentsResult arguments) → Task<IntentResult>` — reads
  `arguments.IntentPath`, validates against `arguments.ProductionPath`. Never throws for an
  unreadable file, malformed JSON, or an invalid contract; other exceptions propagate.
- **Behavior notes for Phase 3 / item 3:** a valid result carries the populated `IntentContract` in
  `Intent` — that is the intent context the gate pipeline consumes. Read/JSON failures carry
  `Intent = null`; validator failures carry the submitted contract. Call `Parse` only with a
  `VerifyArgumentsResult` whose `IsValid` is true.
