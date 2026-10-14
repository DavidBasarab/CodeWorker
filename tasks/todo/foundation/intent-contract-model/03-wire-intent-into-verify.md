# Phase 3 — Wire Intent Parsing into `verify` + E2E Coverage

- **Work item:** intent-contract-model (see
  `tasks/todo/foundation/intent-contract-model/00-overview.md`)
- **Depends on:** Phase 2 (`02-intent-file-parser.md`) (and transitively Phase 1)
- **Depended on by:** — (item 3 `gate-pipeline-framework` builds on the result)
- **Risk:** **Medium** — changes the runtime path of every `verify` invocation and the E2E fixture's
  intent file; the pinned JSON field names become the de-facto input schema the Stop hook and the
  `CLAUDE.md` intent-file rule will target. No auth/network/data surface.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Especially `async.md` (async all the way; no `.Result`/`.Wait()`), `testing.md`, and
`errors-and-logging.md` (log at the action site; Serilog `ILogger`).

Current state you will find (Phases 1–2 committed):

- `CodeWorker.Cli/Commands/Verify/Intent/ParseIntent.cs` —
  `IParseIntent.Parse(VerifyArgumentsResult arguments) → Task<IntentResult>`; never throws for
  predictable intent problems. `IntentResult { IsValid, Error, Message, Intent }`;
  `IntentContract { Why, Class, Responsibility, List<IntentBehavior> Behaviors }`.
- `CodeWorker.Cli/Commands/Verify/VerifyCommand.cs` today:

```csharp
public class VerifyCommand(IParseVerifyArguments parseVerifyArguments, ILogger logger) : IRunVerifyCommand
{
	public Task Execute(string[] args)
	{
		var result = parseVerifyArguments.Parse(args);

		if (!result.IsValid)
		{
			logger.Error("verify: {Reason}", result.Message);

			return Task.CompletedTask;
		}

		logger.Information(
			"verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}",
			result.IntentPath, result.ProductionPath, result.TestsPath
		);

		return Task.CompletedTask;
	}
}
```

- `CodeWorker.Cli.Tests/Commands/Verify/VerifyCommandTests.cs` — fakes `IParseVerifyArguments` with a
  `currentResult` field + `ReturnsLazily`; tests `ParseTheArguments`, `LogTheUsageErrorWhenInvalid`,
  `NotLogAnErrorWhenValid`, `CompleteWhenValid`. Constructor builds `new VerifyCommand(parseVerifyArguments, logger)`.
- **E2E** (`CodeWorker.Cli.EndToEnd.Tests`, run by `dotnet test CodeWorker.sln`; publishes the CLI
  once per run): `Harness/TempWorkspace.cs` writes `intent.json` = `{ "class": "Foo" }`, `Foo.cs`,
  `FooTests.cs` into a temp folder. `VerifyValidInvocationTests` asserts stdout contains
  `"verify: parsed intent"` and the intent path, exit 0. `VerifyUsageErrorTests` asserts usage
  messages. Serilog's console sink writes to **stdout**; string properties render quoted
  (`"Foo"`), numbers unquoted.

## Design (build exactly this shape)

**`VerifyCommand`** gains `IParseIntent` and becomes `async`:

```csharp
using FatCat.CodeWorker.Cli.Commands.Verify.Intent;
using Serilog;

public class VerifyCommand(IParseVerifyArguments parseVerifyArguments, IParseIntent parseIntent, ILogger logger)
	: IRunVerifyCommand
{
	public async Task Execute(string[] args)
	{
		var arguments = parseVerifyArguments.Parse(args);

		if (!arguments.IsValid)
		{
			logger.Error("verify: {Reason}", arguments.Message);

			return;
		}

		logger.Information("verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}", ...);

		var intentResult = await parseIntent.Parse(arguments);

		if (!intentResult.IsValid)
		{
			logger.Error("verify: {Reason}", intentResult.Message);

			return;
		}

		logger.Information(
			"verify: intent for {Class} declares {BehaviorCount} behaviors",
			intentResult.Intent.Class,
			intentResult.Intent.Behaviors.Count
		);
	}
}
```

Extract short private helpers if `Execute` reads long (e.g. `LogParsedArguments`). Process exit code
stays 0 in every case — item 4 owns exit codes. No `CodeWorkerCliModule` change (single-impl
interfaces auto-register).

**E2E fixture** — `Harness/TempWorkspace.cs`: write a **valid** intent for `Foo` by default:

```json
{
  "why": "End-to-end fixture for the verify command.",
  "class": "Foo",
  "responsibility": "Exist so verify has a production class to check.",
  "behaviors": [ { "behavior": "Returns nothing when constructed", "test": "ReturnNothingWhenConstructed" } ]
}
```

and add a public `WriteIntent(string content)` method that overwrites `intent.json` (for invalid-intent
tests). Keep it `[ExcludeFromCodeCoverage]` (it already is).

**New E2E class** — `CodeWorker.Cli.EndToEnd.Tests/VerifyIntentTests.cs`, namespace
`Testing.FatCat.CodeWorker.Cli.EndToEnd`, same attributes/shape as `VerifyValidInvocationTests`
(`[Trait("Category", "EndToEnd")]`, `[Collection(EndToEndCollection.Name)]`, primary constructor
`(PublishedCli publishedCli)`, a `CliProcessRunner runner = new()` field).

**README** — in `CodeWorker.Cli/README.md`, replace the note
`> Schema is close to final; exact field names are settled in Epic **Intent contract model**.` with a
short note that the field names are final, `test` is optional, the `class` must match the production
file name, and each behavior must start with an observable outcome verb (e.g. Returns, Throws, Calls,
Logs — optionally preceded by "Does not"/"Never"). Do not otherwise rewrite the README.

## Steps (TDD — tests first, red before green)

**Unit — `VerifyCommandTests`** (update the constructor to fake `IParseIntent` with a
`currentIntentResult` field + `ReturnsLazily`, defaulting to a valid result whose `Intent` has
`Class = Faker.Create<string>()` and two behaviors; pass it into `VerifyCommand`). Keep the four
existing tests green; add (one assertion each):

1. `ParseTheIntentWithTheParsedArguments` — `parseIntent.Parse(currentResult)` `MustHaveHappenedOnceExactly`.
2. `NotParseTheIntentWhenArgumentsAreInvalid` — invalid args → `Parse` `MustNotHaveHappened`.
3. `LogTheIntentErrorWhenIntentIsInvalid` — invalid intent result →
   `logger.Error(A<string>._, currentIntentResult.Message)` `MustHaveHappenedOnceExactly`.
4. `LogTheIntentClassAndBehaviorCountWhenValid` —
   `logger.Information("verify: intent for {Class} declares {BehaviorCount} behaviors", currentIntentResult.Intent.Class, 2)`
   `MustHaveHappenedOnceExactly`.
5. `NotLogTheIntentSummaryWhenIntentIsInvalid` — invalid intent → that `Information` call `MustNotHaveHappened`.

**E2E — `VerifyIntentTests`** (write first; they fail until the command is wired):

6. `ReportTheIntentClassForAValidIntent` — default workspace → stdout contains `"verify: intent for"`.
7. `ReportMalformedIntentJson` — `WriteIntent("{ not json")` → stdout contains
   `"Intent file is not a valid intent JSON object"`.
8. `ReportANonObservableBehavior` — valid intent but behavior `"Uses a switch"` → stdout contains
   `"is not observable"`.
9. `ReportAClassThatDoesNotMatchTheProductionFile` — `"class": "Bar"` → stdout contains
   `"does not match the production file"`.
10. `ExitZeroForAnInvalidIntent` — malformed intent → exit code 0 (documents the item-4 deferral).

Then implement the `VerifyCommand` change and the `TempWorkspace` change to green, and update the
README note.

**Manual smoke** (record output in the Phase Report): `dotnet run --project CodeWorker.Cli -- verify
--intent <valid.json> --production <Foo.cs> --tests <FooTests.cs>` prints the intent summary; with a
malformed intent it prints the malformed-intent error.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln` — all tests pass, **including the E2E suite** (existing
      `VerifyValidInvocationTests` / `VerifyUsageErrorTests` / `VerifyBannerTests` unchanged and green)
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespaces match folders; constructor injection only; async all the way (no
      `Task.CompletedTask` left behind in an `async` method, no blocking); no banned patterns
- [ ] Review loop until all three pass clean, in order, restarting from the top after any fix:
      `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current branch (`intent-contract-model`), message referencing this
      file; **no push**

Suggested commit message:

```
intent-contract-model phase 3: wire intent parsing into verify + E2E (tasks/todo/foundation/intent-contract-model/03-wire-intent-into-verify.md)
```

## Rollback Procedure

- `git revert <this commit>` — restores the item-1 `VerifyCommand`, the old E2E fixture, and the
  README note. Nothing depends on this phase within the item. If item 3 has been built on top, revert
  it first.
- No data/config/feature-flag steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (unit new/total/passing, E2E new/total/passing); manual
smoke output; deviation log (an empty log is a claim, not a default); open questions/risks for the
reviewer.

## Hand-off

- **After this phase, `verify`** parses arguments, then reads + validates the intent; it logs either
  the specific intent error or `verify: intent for {Class} declares {BehaviorCount} behaviors`, and
  exits 0 in every case.
- **For item 3 (`gate-pipeline-framework`):** `VerifyCommand` holds both the valid
  `VerifyArgumentsResult` and the valid `IntentResult.Intent` (`IntentContract`) at the point the
  pipeline will run — replace the final summary log with the pipeline call there.
- **For the Integration group:** the pinned intent schema is
  `{ why, class, responsibility, behaviors: [ { behavior, test? } ] }`, unknown properties rejected,
  `class` must equal the production file name, behaviors start with an allow-listed outcome verb.
