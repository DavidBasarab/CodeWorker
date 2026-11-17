# verdict-model-reporting — Overview

- **Work item:** `verdict-model-reporting` (Foundation item 4 — see
  [`tasks/todo/foundation/00-epic-plan.md`](../00-epic-plan.md))
- **Source of truth:** `CodeWorker.Cli/README.md` ("Usage", "How Claude actually hits it")
- **Generated:** 2026-11-17-142201
- **Item wave:** 4 — built in parallel with `language-engine-seam` (disjoint files; see the epic plan)

## Work Item

Build a `Verdict` from the gate pipeline result (or from an invalid intent), write it as JSON to the
`--verdict <path>` file, print a human-readable report, and set the process exit code (0 green, 1
blocked, 2 invalid input, 3 unexpected error). See `task.md` for the specification and acceptance
criteria.

### Current state that shapes the design (verified against the code, 2026-11-17)

- **Items 1–3 are built** (item 3 on `gate-pipeline-framework`, PR #16). `CodeWorker.Cli/Commands/Verify/`:
  - `VerifyCommand(IParseVerifyArguments, IParseIntent, IRunGatePipeline, ILogger)` — async `Execute`:
    parse args (error → `logger.Error("verify: {Reason}", …)`, return) → log parsed paths → parse
    intent (error → log, return) → `LogIntentSummary` → `BuildContext` → `await runGatePipeline.Run` →
    `LogPipelineSummary` (`"verify: gate pipeline finished with {GateCount} gates, blocked: {IsBlocked}"`).
  - `ParseVerifyArguments` — `--intent/--production/--tests` via private `TryGetFlagValue`; checks the
    three files exist; `UsageLine` constant
    `"Usage: verify --intent <intent.json> --production <Foo.cs> --tests <FooTests.cs>"`.
    `VerifyArgumentsResult { IsValid, Error, Message, IntentPath, ProductionPath, TestsPath }`.
  - `Gates/`: `GatePipelineResult { IsBlocked; List<GateResult> GateResults }`,
    `GateResult { GateName; Stage; Status; List<GateFinding> Findings; TimeSpan Duration }`,
    `GateFinding { FilePath; int? Line; Reason }`, `GateStage`, `GateStatus { Passed, Warning, Failed, Skipped }`.
  - `Intent/IntentResult { IsValid; Error; Message; Intent }`.
- **No exit code is ever set** — `Program.Main` (CLI and main project) returns `Task`; a caught
  exception is written by `ConsoleLog.WriteException` and the process exits 0.
- **Stdout is noisy**: `SystemScope.Initialize` prints `Using assembly …` lines via `ConsoleLog`, the
  app logs `Welcome to Code Worker CLI`, and Serilog's console sink (`WriteTo.Console()` in
  `CodeWorkerCliModule`) writes every log line to stdout. Serilog renders string properties
  **quoted** unless the `:l` format is used (`{Report:l}`).
- **`IFileSystemTools`** (FatCat.Toolkit) has `Task WriteAllText(string path, string text)`.
- **E2E** (`CodeWorker.Cli.EndToEnd.Tests`) asserts exit code 0 in four places that this item changes
  or keeps: `VerifyBannerTests.ExitZeroWithNoArguments` (→ 2), `VerifyUsageErrorTests.ExitZeroOnAUsageError`
  (→ 2), `VerifyIntentTests.ExitZeroForAnInvalidIntent` (→ 2), `VerifyGatePipelineTests.ExitZeroWhenThePipelineFinishes`
  and `VerifyValidInvocationTests.ExitZeroForAValidInvocation` (stay 0 — zero gates is green).
  Usage tests assert only the prefix `"Usage: verify --intent"`. `Harness/TempWorkspace` keeps its
  folder in a private `root` and has no verdict path.

## Acceptance Criteria → Phase Map

| Acceptance criterion (task.md) | Proven by |
|---|---|
| `--verdict <path>` optional flag; usage line mentions it | Phase 3 |
| green / blocked / invalidIntent verdicts | Phase 1 |
| Blocked message names first failed gate + first finding | Phase 1 |
| Gates in run order with name, stage, status, ms, findings | Phase 1 |
| JSON: camelCase, camelCase enum strings, nulls omitted | Phase 4 |
| JSON written to the `--verdict` path only when given | Phase 6 |
| Human report: status line, gate lines, finding lines | Phase 5 (format) + Phase 6 (printed) |
| Exit codes 0 / 1 / 2 / 3 | Phase 2 (abstraction + crash code) + Phase 6 (mapping + usage error) |
| E2E green / invalidIntent / usage error / report line | Phase 6 |

## Phases & Dependency Graph

| Phase | File | Risk | Depends on | Depended on by |
|---|---|---|---|---|
| 1 — Verdict model + builder (`Verdicts/`: `VerdictStatus`, `GateVerdict`, `Verdict`, `IBuildVerdict`/`BuildVerdict`) | `01-verdict-model-and-builder.md` | Low — new isolated types + pure mapping | — | 4, 5, 6 |
| 2 — Exit codes (`CliExitCode`, `Process/SetExitCode.cs`, crash exit code in `Program.Main`) | `02-exit-codes.md` | Medium — changes the process contract every caller sees on a crash | — | 6 |
| 3 — `--verdict <path>` flag (`ParseVerifyArguments`, `VerifyArgumentsResult.VerdictPath`) | `03-verdict-flag.md` | **High** — public CLI contract (auto-high: public API) | — | 6 |
| 4 — JSON verdict serializer (`ISerializeVerdict`/`SerializeVerdict`) | `04-verdict-json.md` | **High** — defines the JSON the Stop hook parses (public contract) | 1 | 6 |
| 5 — Human report formatter (`IFormatVerdictReport`/`FormatVerdictReport`) | `05-verdict-report.md` | Low — pure string formatting | 1 | 6 |
| 6 — Deliver the verdict + wire into `verify` + E2E (`IDeliverVerdict`/`DeliverVerdict`, `VerifyCommand`, E2E) | `06-deliver-verdict.md` | **High** — changes exit codes and outputs of every `verify` call (public contract) | 1, 2, 3, 4, 5 | — |

**Revert cascade:** revert 6 first (it uses everything). Then 4 and 5 (independent), then 1, 2, 3 in
any order.

### Execution waves (parallel subagents)

| Wave | Phases | Runs as | Why parallel-safe |
|---|---|---|---|
| 1 | 1, 2, 3 | **Three parallel worktree subagents** | No dependencies between them; disjoint write sets (below); no shared hotspot |
| 2 | 4, 5 | **Two parallel worktree subagents** | Both depend only on 1; disjoint write sets; no hotspot |
| 3 | 6 | Single subagent on the task branch | Needs all five; edits the E2E harness hotspot |

**Critical path:** 1 → 4 → 6 (three sequential steps).

Write sets:

| Phase | Creates | Modifies |
|---|---|---|
| 1 | `CodeWorker.Cli/Commands/Verify/Verdicts/{VerdictStatus,GateVerdict,Verdict,BuildVerdict}.cs`, `CodeWorker.Cli.Tests/Commands/Verify/Verdicts/BuildVerdictTests.cs` | — |
| 2 | `CodeWorker.Cli/CliExitCode.cs`, `CodeWorker.Cli/Process/SetExitCode.cs` | `CodeWorker.Cli/Program.cs` |
| 3 | — | `CodeWorker.Cli/Commands/Verify/ParseVerifyArguments.cs`, `CodeWorker.Cli/Commands/Verify/VerifyArgumentsResult.cs`, `CodeWorker.Cli.Tests/Commands/Verify/ParseVerifyArgumentsTests.cs` |
| 4 | `CodeWorker.Cli/Commands/Verify/Verdicts/SerializeVerdict.cs`, `CodeWorker.Cli.Tests/Commands/Verify/Verdicts/SerializeVerdictTests.cs` | — |
| 5 | `CodeWorker.Cli/Commands/Verify/Verdicts/FormatVerdictReport.cs`, `CodeWorker.Cli.Tests/Commands/Verify/Verdicts/FormatVerdictReportTests.cs` | — |
| 6 | `CodeWorker.Cli/Commands/Verify/Verdicts/DeliverVerdict.cs`, its tests, `CodeWorker.Cli.EndToEnd.Tests/VerifyVerdictTests.cs` | `VerifyCommand.cs`, `VerifyCommandTests.cs`, `Harness/TempWorkspace.cs`, `VerifyBannerTests.cs`, `VerifyUsageErrorTests.cs`, `VerifyIntentTests.cs`, `CodeWorker.Cli/README.md` (Usage) |

**How the split maximizes parallelism.** Exit codes (2) and the CLI flag (3) don't depend on the
verdict types, so they run beside the model (1) instead of after it. Serialization (4) and the report
(5) are two independent consumers of the model, so they fan out together. Everything that needs all of
them — delivering the verdict and the `VerifyCommand` / E2E edits — is gathered into one final phase,
because `VerifyCommand.cs` and the E2E files can only be edited by one phase at a time. Splitting
`DeliverVerdict` from the wiring would add a fourth wave without removing any work from the critical path.

## Orchestrator

`orchestrator.md` (this folder). Say **"run verdict-model-reporting"** — wave 1 (3 parallel worktree
subagents) → integrate → wave 2 (2 parallel) → integrate → wave 3 (1). A watchdog runs throughout.

## Decisions (lightweight ADRs)

### ADR-1 — `Verdict` is its own model, not the raw pipeline result
**Decision:** `Verdict { VerdictStatus Status; string Message; List<GateVerdict> Gates = [] }`,
`GateVerdict { string Name; GateStage Stage; GateStatus Status; long DurationMilliseconds; List<GateFinding> Findings = [] }`,
`VerdictStatus { Green, Blocked, InvalidIntent }` in `Commands/Verify/Verdicts/` (ADR-G3).
The folder is plural (`Verdicts`, like `Gates`, `Engines`) because a `Verdict` class inside a `Verdict`
namespace makes the bare name resolve to the namespace (CS0118) from `…Commands.Verify` and the test
namespaces — found by Phase 1 during the build (2026-11-17).
**Context:** The JSON is a contract the `Stop` hook parses; `TimeSpan` would serialize as
`"00:00:00.0123"`, and `IsBlocked` alone can't express an invalid intent. A dedicated model keeps the
wire shape stable while `GateResult` evolves.
**Alternatives rejected:** serializing `GatePipelineResult` directly (unstable wire shape, no
invalid-intent case); a `Duration` string (harder for a hook script than milliseconds).

### ADR-2 — The verdict message is what the AI fixes next
**Decision:** Green → `"No gate failed."`; Blocked → `"{GateName} failed: {Reason} ({FilePath}:{Line})"`
from the **first** failed gate's **first** finding (`:{Line}` omitted when `Line` is null; the
parenthesized location omitted when `FilePath` is null; `"{GateName} failed."` when it has no
findings); InvalidIntent → the intent error's `Message`.
**Context:** README: a failed gate gives "the exact gate, location, and reason". The hook will use the
message as the `reason` it hands back to Claude.
**Alternatives rejected:** concatenating every finding (noisy — the pipeline already stops at the
first failure, and the full list is in `gates`).

### ADR-3 — Distinct exit codes through an `ISetExitCode` abstraction
**Decision:** `CliExitCode { Success = 0, Blocked = 1, InvalidInput = 2, UnexpectedError = 3 }` in
`CodeWorker.Cli/CliExitCode.cs` (CLI-wide, not verify-specific). `ISetExitCode.Set(CliExitCode)` →
`SetExitCode` writes `Environment.ExitCode` — a `[ExcludeFromCodeCoverage]` wrapper in
`CodeWorker.Cli/Process/` (the house folder for process-level abstractions, as in the main project).
`Program.Main`'s catch sets `Environment.ExitCode = (int)CliExitCode.UnexpectedError` directly (it is
the bootstrap boundary, not injectable).
**Context:** README: "Exit code 0 — green… Non-zero exit — a gate failed." Distinct codes let the hook
tell a red gate (feed back to Claude) from a broken invocation (a hook bug). Process-wide state must be
fakeable (`naming-and-structure.md`: "If something cannot be faked in a test, it is not properly abstracted").
**Alternatives rejected:** changing `ICommand.Execute` to return `Task<int>` (touches `ICommand`,
`ProcessArguments`, `CodeWorkerCliApplication`, `Program` and every command for one value);
`Environment.Exit` (kills the process before Serilog flushes); one non-zero code for everything.

### ADR-4 — `--verdict <path>` is optional and not existence-checked (user decision 2026-11-17)
**Decision:** `ParseVerifyArguments` reads `--verdict` with the existing `TryGetFlagValue`; absent →
`VerdictPath = null` (still valid). It is **not** passed to `FileExists` — it is an output. The usage
line becomes `"Usage: verify --intent <intent.json> --production <Foo.cs> --tests <FooTests.cs> [--verdict <verdict.json>]"`.
A `--verdict` with no value is treated as absent (same rule the other flags use).
**Context:** The user chose a verdict file over parsing stdout. Optional keeps manual runs simple.
**Alternatives rejected:** a fixed file next to the intent (dirties the repo the `Stop` hook diffs);
a required flag (breaks manual use and every existing E2E invocation).

### ADR-5 — JSON: camelCase names, camelCase enum strings, nulls omitted, indented
**Decision:** `SerializeVerdict` uses a `private static readonly JsonSerializerOptions` with
`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, `Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }`,
`DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`, `WriteIndented = true`. Example:
```json
{
  "status": "blocked",
  "message": "Compile failed: CS1002 ; expected (Foo.cs:12)",
  "gates": [
    { "name": "Compile", "stage": "compile", "status": "failed", "durationMilliseconds": 120,
      "findings": [ { "filePath": "Foo.cs", "line": 12, "reason": "CS1002 ; expected" } ] },
    { "name": "Test", "stage": "test", "status": "skipped", "durationMilliseconds": 0, "findings": [] }
  ]
}
```
**Context:** The hook is a script — readable names and string enums beat integers. House library is
`System.Text.Json` (ADR-G4).
**Alternatives rejected:** integer enums (opaque to the hook); `IJsonOperations` (adds converters
we don't want — same reasoning as item 2's ADR-5).

### ADR-6 — `DeliverVerdict` owns "write, print, exit code"; `VerifyCommand` stays a coordinator
**Decision:** `IDeliverVerdict.Deliver(Verdict verdict, string verdictPath)`: when `verdictPath` is not
blank, `await fileSystemTools.WriteAllText(verdictPath, serializeVerdict.Serialize(verdict))`; then
`logger.Information("{Report:l}", formatVerdictReport.Format(verdict))`; then
`setExitCode.Set(...)` mapped by a switch expression (Green → Success, Blocked → Blocked,
InvalidIntent → InvalidInput, `_` → throw `ArgumentOutOfRangeException`). `VerifyCommand` calls it
for the pipeline verdict and for an invalid intent, and calls `setExitCode.Set(InvalidInput)` itself
on a usage error (no verdict — the flags may not have parsed). A write failure (bad path) is not
caught — it is a hook misconfiguration and surfaces as exit 3.
**Context:** Keeps `VerifyCommand` a short sequence of well-named steps (`naming-and-structure.md`).
**Alternatives rejected:** all of it inline in `VerifyCommand` (seven responsibilities in one class);
catching the write `IOException` (would hide a broken hook behind a green-looking console).

## Assumptions

- Nullable disabled; `int? Line` stays the only nullable value type in the model.
- Serilog's `:l` format specifier prints a string property without quotes (`{Report:l}`).
- `JsonStringEnumConverter(JsonNamingPolicy)` and `JsonIgnoreCondition.WhenWritingNull` are available
  on net11.0.
- The task branch is `verdict-model-reporting`; the orchestrator works on the current branch and
  stops if it is `main`. Item 5 (`language-engine-seam`) is built at the same time from a **separate
  worktree checkout** — never in this working directory.

## Open Questions

None blocking. For the human reviewer:

- **Blocked report line level.** The report logs at `Information` for every status; the Integration
  group may prefer `Warning`/`Error` for blocked once the hook exists.
- **`LogPipelineSummary` stays.** The item-3 summary line is kept (its E2E asserts it); it can be
  dropped once the report is trusted.
- **Write failure → exit 3.** If the hook should get a clearer signal for a bad `--verdict` path,
  a `VerdictPathUnwritable` usage error could be added later.
