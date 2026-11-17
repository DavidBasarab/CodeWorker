# Phase 3 — Run the Gate Pipeline from `verify` + E2E

- **Work item:** gate-pipeline-framework (see
  `tasks/todo/foundation/gate-pipeline-framework/00-overview.md`)
- **Depends on:** Phase 1 (`01-gate-contract-and-runner.md`)
- **Depended on by:** — (item 4 `verdict-model-reporting` builds on the pipeline result here)
- **Wave:** 2
- **Parallel with:** Phase 2 — no dependency between them, disjoint write sets, no shared hotspot
  (the E2E harness is read, not edited).
- **Risk:** **Medium** — changes the runtime path of every `verify` invocation; the summary line is
  what the E2E suite asserts and what item 4 replaces with the verdict. No auth/network/data surface.

## Touches

- Creates: `CodeWorker.Cli.EndToEnd.Tests/VerifyGatePipelineTests.cs`
- Modifies: `CodeWorker.Cli/Commands/Verify/VerifyCommand.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/VerifyCommandTests.cs`
- Exclusive runtime resources: none (the E2E suite publishes into this worktree's own output folder)

You run in your own worktree while Phase 2 edits `RunGatePipeline.cs` / `RunGatePipelineTests.cs`
and adds `Time/SystemClock.cs`. Create or modify **only** the files above — nothing under
`Commands/Verify/Gates/` and nothing in `Harness/`.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Especially `async.md`, `testing.md`, and `errors-and-logging.md`.

Current state you will find (Phase 1 committed):

- `CodeWorker.Cli/Commands/Verify/Gates/` — `IRunGatePipeline.Run(VerificationContext) → Task<GatePipelineResult>`;
  `GatePipelineResult { bool IsBlocked; List<GateResult> GateResults }`;
  `VerificationContext { IntentPath; ProductionPath; TestsPath; IntentContract Intent }`. There are
  **zero concrete gates**, so the real runner returns `IsBlocked = false` and an empty list.
- `CodeWorker.Cli/Commands/Verify/VerifyCommand.cs`:
  `VerifyCommand(IParseVerifyArguments parseVerifyArguments, IParseIntent parseIntent, ILogger logger)`;
  `async Execute(string[] args)` parses args (error → log + return), logs parsed paths, awaits
  `parseIntent.Parse(arguments)` (error → log + return), then calls private
  `LogIntentSummary(intentResult.Intent)`, which logs
  `"verify: intent for {Class} declares {BehaviorCount} behaviors"`.
- `CodeWorker.Cli.Tests/Commands/Verify/VerifyCommandTests.cs` — fakes `IParseVerifyArguments` and
  `IParseIntent` with `currentResult` / `currentIntentResult` fields + `ReturnsLazily`; helpers
  `ValidResult()`, `InvalidResult()`, `ValidIntentResult()`, `InvalidIntentResult()`.
- E2E (`CodeWorker.Cli.EndToEnd.Tests`): `VerifyIntentTests.cs` is the pattern to copy —
  `[Trait("Category", "EndToEnd")]`, `[Collection(EndToEndCollection.Name)]`, primary constructor
  `(PublishedCli publishedCli)`, `private readonly CliProcessRunner runner = new();`,
  `using var workspace = new TempWorkspace();` (writes a **valid** `Foo` intent by default),
  `workspace.WriteIntent(string)` to overwrite it, and a private `RunVerify(workspace)` helper that
  calls `runner.Run(publishedCli.ExecutablePath, "verify", "--intent", …, "--production", …, "--tests", …)`.
  Serilog writes to stdout: numbers render bare (`0`), booleans as `False`/`True`.
- DI: `IRunGatePipeline` → `RunGatePipeline` resolves by scanning; no module change.

## Design (build exactly this shape)

**Modify** `VerifyCommand` — add `IRunGatePipeline runGatePipeline` to the primary constructor
(`using FatCat.CodeWorker.Cli.Commands.Verify.Gates;`). After `LogIntentSummary(...)`:

```csharp
var context = BuildContext(arguments, intentResult.Intent);

var pipelineResult = await runGatePipeline.Run(context);

LogPipelineSummary(pipelineResult);
```

- `BuildContext(VerifyArgumentsResult arguments, IntentContract intent)` → `new VerificationContext { IntentPath, ProductionPath, TestsPath, Intent }`.
- `LogPipelineSummary(GatePipelineResult result)` →
  `logger.Information("verify: gate pipeline finished with {GateCount} gates, blocked: {IsBlocked}", result.GateResults.Count, result.IsBlocked);`
- Keep the existing intent summary log. The pipeline never runs when args or intent are invalid
  (the existing early returns already guarantee it). Exit code stays 0 — item 4 owns it.

**New** `CodeWorker.Cli.EndToEnd.Tests/VerifyGatePipelineTests.cs`, namespace
`Testing.FatCat.CodeWorker.Cli.EndToEnd`, same attributes and shape as `VerifyIntentTests`.

## Steps (TDD — tests first, red before green)

**Unit — `VerifyCommandTests`:** add `private readonly IRunGatePipeline runGatePipeline;` (fake), a
`private GatePipelineResult currentPipelineResult;` defaulting to
`new GatePipelineResult { IsBlocked = false, GateResults = [] }`, wire
`A.CallTo(() => runGatePipeline.Run(A<VerificationContext>._)).ReturnsLazily(() => currentPipelineResult)`,
and pass the fake into the constructor. **All existing tests stay green** apart from the constructor
call. Add, one assertion each:

1. `RunTheGatePipelineWhenTheIntentIsValid` — `Run` `MustHaveHappenedOnceExactly`.
2. `NotRunTheGatePipelineWhenArgumentsAreInvalid` — `currentResult = InvalidResult()` → `MustNotHaveHappened`.
3. `NotRunTheGatePipelineWhenTheIntentIsInvalid` — `currentIntentResult = InvalidIntentResult()` → `MustNotHaveHappened`.
4. `PassTheParsedPathsAndIntentInTheContext` — capture the context
   (`.Invokes((VerificationContext c) => capturedContext = c)`) and assert
   `capturedContext.Should().BeEquivalentTo(new VerificationContext { IntentPath = currentResult.IntentPath, ProductionPath = currentResult.ProductionPath, TestsPath = currentResult.TestsPath, Intent = currentIntentResult.Intent })`.
5. `LogThePipelineSummary` — `currentPipelineResult` with `IsBlocked = true` and two
   `Faker.Create<GateResult>()` results →
   `logger.Information("verify: gate pipeline finished with {GateCount} gates, blocked: {IsBlocked}", 2, true)`
   `MustHaveHappenedOnceExactly`.

**E2E — `VerifyGatePipelineTests`** (write first; they fail until the command is wired):

6. `ReportThePipelineSummaryForAValidInvocation` — stdout contains `"verify: gate pipeline finished with 0 gates"`.
7. `ReportTheEmptyPipelineAsNotBlocked` — stdout contains `"blocked: False"`.
8. `NotRunThePipelineForAnInvalidIntent` — `workspace.WriteIntent("{ not json")` → stdout does not
   contain `"verify: gate pipeline"` (FatCat.Testing negation: `.Should().Not.Contain(...)`).
9. `ExitZeroWhenThePipelineFinishes` — valid invocation → exit code 0 (documents the item-4 deferral).

Red first, then implement the `VerifyCommand` change to green.

**Manual smoke** (record in the Phase Report):
`dotnet run --project CodeWorker.Cli -- verify --intent <valid.json> --production <Foo.cs> --tests <FooTests.cs>`
prints the intent summary followed by `verify: gate pipeline finished with 0 gates, blocked: False`.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass, **including the E2E suite** (existing
      `VerifyValidInvocationTests`, `VerifyUsageErrorTests`, `VerifyBannerTests`, `VerifyIntentTests` unchanged and green)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; constructor injection only; async all the way; no banned patterns
- [ ] Only the files named in Design were created/modified (wave-2 disjointness)
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the isolated worktree branch the orchestrator gave you, message referencing this file; no push

Suggested commit message:

```
gate-pipeline-framework phase 3: run the gate pipeline from verify + E2E (tasks/todo/foundation/gate-pipeline-framework/03-wire-pipeline-into-verify.md)
```

## Rollback Procedure

- `git revert <this phase's commit>` — restores `VerifyCommand` without the pipeline and removes the
  E2E class. If item 4 has been built on top, revert it first. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (unit new/total/passing, E2E new/total/passing); manual
smoke output; deviation log (an empty log is a claim, not a default); open questions/risks for the
reviewer.

## Hand-off

- **After this phase, `verify`** parses arguments, validates the intent, builds a
  `VerificationContext`, runs the gate pipeline, and logs
  `verify: gate pipeline finished with {GateCount} gates, blocked: {IsBlocked}`; exit code 0 always.
- **For item 4 (`verdict-model-reporting`):** `VerifyCommand` holds the `GatePipelineResult` at the
  point the verdict is built — replace `LogPipelineSummary` with the verdict build, JSON/report
  output, and exit code there.
- **For item 5 (`language-engine-seam`) and the gate groups:** concrete gates implement
  `IVerificationGate` and are picked up by scanning with no change to `VerifyCommand` or the runner.
