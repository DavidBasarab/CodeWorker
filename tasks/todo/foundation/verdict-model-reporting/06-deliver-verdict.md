# Phase 6 — Deliver the Verdict + Wire into `verify` + E2E

- **Work item:** verdict-model-reporting (see `tasks/todo/foundation/verdict-model-reporting/00-overview.md`)
- **Depends on:** Phases 1, 2, 3, 4, 5
- **Depended on by:** —
- **Wave:** 3
- **Parallel with:** none — needs every earlier phase, and edits `VerifyCommand.cs` and the E2E
  harness (hotspot), which no other phase may touch at the same time.
- **Risk:** **High** (auto: public contract) — changes the exit code and outputs of every `verify` call.

## Touches

- Creates: `CodeWorker.Cli/Commands/Verify/Verdict/DeliverVerdict.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/Verdict/DeliverVerdictTests.cs`,
  `CodeWorker.Cli.EndToEnd.Tests/VerifyVerdictTests.cs`
- Modifies: `CodeWorker.Cli/Commands/Verify/VerifyCommand.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/VerifyCommandTests.cs`,
  `CodeWorker.Cli.EndToEnd.Tests/Harness/TempWorkspace.cs`,
  `CodeWorker.Cli.EndToEnd.Tests/VerifyBannerTests.cs`,
  `CodeWorker.Cli.EndToEnd.Tests/VerifyUsageErrorTests.cs`,
  `CodeWorker.Cli.EndToEnd.Tests/VerifyIntentTests.cs`,
  `CodeWorker.Cli/README.md` (Usage section only)
- Exclusive runtime resources: none

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("Usage", "How Claude actually hits it"), `CLAUDE.md`, and **all**
`.claude/rules/csharp/*.md` first — mandatory.

Current state (Phases 1–5 integrated on the task branch):
- `Verdict/`: `Verdict`, `GateVerdict`, `VerdictStatus { Green, Blocked, InvalidIntent }`;
  `IBuildVerdict.Build(GatePipelineResult)` / `BuildInvalidIntent(IntentResult)`;
  `ISerializeVerdict.Serialize(Verdict) → string`; `IFormatVerdictReport.Format(Verdict) → string`
  (first line `Verdict: GREEN|BLOCKED|INVALID INTENT — {message}`).
- `CliExitCode { Success = 0, Blocked = 1, InvalidInput = 2, UnexpectedError = 3 }` (namespace
  `FatCat.CodeWorker.Cli`); `ISetExitCode.Set(CliExitCode)` (namespace `FatCat.CodeWorker.Cli.Process`).
- `VerifyArgumentsResult.VerdictPath` — the `--verdict` value or null.
- `VerifyCommand(IParseVerifyArguments, IParseIntent, IRunGatePipeline, ILogger)` — see its current
  `Execute`: parse args → error log + return; log paths; parse intent → error log + return;
  `LogIntentSummary`; `BuildContext`; `await runGatePipeline.Run`; `LogPipelineSummary`.
- `VerifyCommandTests` — `current*` fields + `ReturnsLazily` fakes for each collaborator.
- `IFileSystemTools.WriteAllText(string path, string text)` → `Task` (FatCat.Toolkit).
- E2E: `Harness/TempWorkspace` (private `root` folder; `IntentPath`, `ProductionPath`, `TestsPath`,
  `WriteIntent`), `CliProcessRunner.Run(exe, params string[] args)` → `CliResult { ExitCode, StandardOutput, StandardError }`.
  Tests that assert exit 0 today: `VerifyBannerTests.ExitZeroWithNoArguments`,
  `VerifyUsageErrorTests.ExitZeroOnAUsageError`, `VerifyIntentTests.ExitZeroForAnInvalidIntent`
  (all three become 2), `VerifyGatePipelineTests.ExitZeroWhenThePipelineFinishes` and
  `VerifyValidInvocationTests.ExitZeroForAValidInvocation` (stay 0 — no gates is green; do not touch).

## Design (build exactly this shape)

**`Verdict/DeliverVerdict.cs`** — interface + class:
```csharp
public interface IDeliverVerdict
{
	Task Deliver(Verdict verdict, string verdictPath);
}

public class DeliverVerdict(
	ISerializeVerdict serializeVerdict,
	IFormatVerdictReport formatVerdictReport,
	IFileSystemTools fileSystemTools,
	ISetExitCode setExitCode,
	ILogger logger
) : IDeliverVerdict
```
`Deliver`: when `verdictPath` is not null/whitespace → `await fileSystemTools.WriteAllText(verdictPath, serializeVerdict.Serialize(verdict))`;
then `logger.Information("{Report:l}", formatVerdictReport.Format(verdict))`; then
`setExitCode.Set(ExitCodeFor(verdict.Status))` with a switch expression: Green → `Success`,
Blocked → `Blocked`, InvalidIntent → `InvalidInput`, `_` → throw `ArgumentOutOfRangeException`.
Do not catch write exceptions (ADR-6).

**`VerifyCommand`** gains `IBuildVerdict buildVerdict`, `IDeliverVerdict deliverVerdict`,
`ISetExitCode setExitCode`:
- invalid arguments → keep the error log, then `setExitCode.Set(CliExitCode.InvalidInput)`; return.
- invalid intent → keep the error log, then
  `await deliverVerdict.Deliver(buildVerdict.BuildInvalidIntent(intentResult), arguments.VerdictPath)`; return.
- after `LogPipelineSummary` → `await deliverVerdict.Deliver(buildVerdict.Build(pipelineResult), arguments.VerdictPath)`.
Keep `Execute` a short sequence of named steps.

**E2E harness** — `TempWorkspace` gains `public string VerdictPath { get; }` =
`Path.Combine(root, "verdict.json")` (not created).

**README** — in `CodeWorker.Cli/README.md` "Usage": add `[--verdict verdict.json]` to the example,
replace the two exit-code bullets with the 0/1/2/3 table from `task.md`, and say the JSON verdict is
written to the `--verdict` file. Do not otherwise rewrite the README.

## Steps (TDD — tests first, red before green)

**Unit — `DeliverVerdictTests`** (fakes for all five collaborators; `Serialize` returns a
`Faker.Create<string>()`, `Format` likewise). One assertion each:
1. `WriteTheSerializedVerdictToTheVerdictPath`
2. `NotWriteAVerdictFileWhenNoPathIsGiven` — null path → `WriteAllText` `MustNotHaveHappened`.
3. `NotWriteAVerdictFileWhenThePathIsBlank`
4. `LogTheFormattedReport` — `logger.Information("{Report:l}", report)`.
5. `ExitWithSuccessWhenGreen`, 6. `ExitWithBlockedWhenBlocked`, 7. `ExitWithInvalidInputWhenTheIntentIsInvalid`

**Unit — `VerifyCommandTests`** (add the three fakes + defaults; existing tests stay green apart from
the constructor call):
8. `ExitWithInvalidInputWhenArgumentsAreInvalid`
9. `NotDeliverAVerdictWhenArgumentsAreInvalid`
10. `DeliverTheInvalidIntentVerdictWhenTheIntentIsInvalid` — `Deliver(theBuiltInvalidVerdict, currentResult.VerdictPath)`.
11. `NotRunTheGatePipelineWhenTheIntentIsInvalid` (exists — keep)
12. `BuildTheVerdictFromThePipelineResult` — `Build(currentPipelineResult)`.
13. `DeliverThePipelineVerdictToTheVerdictPath`

**E2E — `VerifyVerdictTests`** (new; same shape as `VerifyGatePipelineTests`; pass
`"--verdict", workspace.VerdictPath`; read the file with `File.ReadAllText` and parse with
`JsonDocument`):
14. `WriteAGreenVerdictForAValidInvocation` — `status` is `"green"`.
15. `WriteAnInvalidIntentVerdict` — malformed intent → `status` is `"invalidIntent"`.
16. `ExitTwoForAnInvalidIntentWithAVerdict`
17. `WriteNoVerdictFileOnAUsageError` — omit `--tests` but pass `--verdict` → file does not exist.
18. `PrintTheVerdictReport` — stdout contains `"Verdict: GREEN"`.

**E2E — update** (rename + new expectation, one assertion each):
`VerifyBannerTests.ExitZeroWithNoArguments` → `ExitTwoWithNoArguments` (2);
`VerifyUsageErrorTests.ExitZeroOnAUsageError` → `ExitTwoOnAUsageError` (2);
`VerifyIntentTests.ExitZeroForAnInvalidIntent` → `ExitTwoForAnInvalidIntent` (2).

Red first (record which tests failed and why), then implement to green.

**Manual smoke** (record): a valid run with `--verdict` writes `"status": "green"`, prints
`Verdict: GREEN — No gate failed.`, exits 0; a malformed intent exits 2.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass, **including the full E2E suite**
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; constructor injection only; async all the way; switch expression with throwing discard; no banned patterns
- [ ] Only the files in `## Touches` created/modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review` (screen-only — write nothing outside `## Touches`)
- [ ] Exactly one commit on the task branch, message referencing this file; no push

Suggested commit message:

```
verdict-model-reporting phase 6: deliver the verdict + exit codes in verify (tasks/todo/foundation/verdict-model-reporting/06-deliver-verdict.md)
```

## Rollback Procedure

- `git revert <this commit>` — `verify` returns to logging only and exit 0. Nothing in this item
  depends on it. If Integration-group work (the `Stop` hook) has been built on the verdict, revert it first.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (unit and E2E, new/total/passing); manual smoke output;
deviation log (an empty log is a claim, not a default); open questions/risks.

## Hand-off

- **`verify` contract** (for the Integration group's `Stop` hook): pass `--verdict <temp path>`; read
  the JSON (`status` = `green` | `blocked` | `invalidIntent`, `message`, `gates[]`); exit 0 green,
  1 blocked, 2 usage error / invalid intent, 3 unexpected error. A usage error writes no verdict file.
- The human report's first line is `Verdict: <STATUS> — <message>` on stdout.
