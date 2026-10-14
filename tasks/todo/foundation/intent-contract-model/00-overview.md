# intent-contract-model — Overview

- **Work item:** `intent-contract-model` (Foundation item 2 — see
  [`tasks/todo/foundation/00-epic-plan.md`](../00-epic-plan.md))
- **Source of truth:** `CodeWorker.Cli/README.md` ("The Intent Contract")
- **Generated:** 2026-10-14-125623

## Work Item

Read the intent file named by `verify --intent`, deserialize it into an **`IntentContract`**
(`why`, `class`, `responsibility`, `behaviors[]` of `{ behavior, test? }`), and validate it into an
**`IntentResult`** — either valid (carrying the populated contract the pipeline consumes) or a
single, tagged **`IntentValidationError`** with a human-readable message. Every predictable failure
(unreadable file, malformed JSON, invalid contract) is a **returned value, never an exception**.

### Current state that shapes the design (verified against the code, 2026-10-14)

- **Item 1 is built and merged** (PR #12). `CodeWorker.Cli/Commands/Verify/` contains:
  - `VerifyCommand.cs` — `IRunVerifyCommand : ICommand` + `VerifyCommand(IParseVerifyArguments, ILogger)`.
    `Execute` is **not** `async`: it parses args, logs `"verify: {Reason}"` at `Error` on failure,
    otherwise logs `"verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}"`
    at `Information`, and returns `Task.CompletedTask`.
  - `ParseVerifyArguments.cs` — `IParseVerifyArguments.Parse(string[]) → VerifyArgumentsResult`; it
    has already proven all three files **exist** via `IFileSystemTools.FileExists`.
  - `VerifyArgumentsResult.cs` — `{ IsValid, Error, Message, IntentPath, ProductionPath, TestsPath }`.
  - `VerifyUsageError.cs` — the item-1 enum. **This item adds a separate enum** (see ADR-2).
- **No `Commands/Verify/Intent/` folder exists yet.** ADR-G3 places this item's types there,
  namespace `FatCat.CodeWorker.Cli.Commands.Verify.Intent`.
- **`IFileSystemTools`** (`FatCat.Toolkit`) exposes `Task<string> ReadAllText(string path)` (verified
  in `C:\Code\FatCat.Toolkit\src\ToolKit\FileSystemTools.cs`) and auto-registers via `SystemScope`.
- **House JSON pattern** — `CodeWorker/Commands/Run/LoadRepoSettings.cs`:
  `await fileSystemTools.ReadAllText(path)` then
  `JsonSerializer.Deserialize<T>(json, JsonOptions)` with
  `private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };`.
  The main project uses `System.Text.Json` directly (not the toolkit's `IJsonOperations`). Match it.
- **`CodeWorkerCliModule`** registers only the Serilog `ILogger`; no module change is needed (every
  new interface has one implementation — ADR-G5).
- **E2E suite** (`CodeWorker.Cli.EndToEnd.Tests`, in `CodeWorker.sln`, runs under `dotnet test`)
  publishes the CLI and drives it as a process. `Harness/TempWorkspace.cs` currently writes the intent
  file as `{ "class": "Foo" }` — **invalid under this item's rules** (no `why`, no behaviors). Phase 3
  must make the workspace write a valid intent, or the green-path E2E would start logging an intent
  error. Existing E2E assertions (`"verify: parsed intent"`, usage errors, exit 0) must stay green.
- **Test stack** (`CodeWorker.Cli.Tests/GlobalUsings.cs`): xUnit, FakeItEasy, FluentAssertions,
  `FatCat.Fakes`. Tests mirror source folders with the `Testing.` prefix.

## Acceptance Criteria → Phase Map

| Acceptance criterion (task.md) | Proven by |
|---|---|
| Valid intent JSON → valid result with a populated `IntentContract` | Phase 1 (validator accepts a valid contract and returns it) + Phase 2 (round-trip from sample JSON) |
| Case-insensitive property names; unknown properties rejected as malformed | Phase 2 (`JsonOptions` + tests) |
| Unreadable file → `IntentFileUnreadable`, no exception | Phase 2 |
| Malformed JSON / `null` / non-object → `MalformedIntentJson`, no exception | Phase 2 |
| Each invalid contract → distinct `IntentValidationError` | Phase 1 (one test per error) |
| Every failure carries a specific message (field / 1-based behavior number + text) | Phase 1 + Phase 2 |
| One error at a time in fixed precedence | Phase 1 (precedence tests) + Phase 2 (read → JSON before validation) |
| `verify` reads + validates the intent; logs error or class + behavior count | Phase 3 (unit + E2E) |
| Unit-tested against sample JSON with faked `IFileSystemTools`; E2E valid + invalid intent; zero warnings; green | Phase 2 + Phase 3 + every phase's Definition of Done |

## Phases & Dependency Graph

| Phase | File | Risk | Depends on | Depended on by |
|---|---|---|---|---|
| 1 — Intent model + contract validator (`IntentBehavior`, `IntentContract`, `IntentValidationError`, `IntentResult`, `IValidateIntent`/`ValidateIntent`) | `01-intent-model-validation.md` | **Low** — new, isolated value types + a pure validator; not wired anywhere | — | 2 |
| 2 — Intent file parser (`IParseIntent`/`ParseIntent`: read via `IFileSystemTools`, deserialize via `System.Text.Json`, map read/JSON failures to values, delegate to `IValidateIntent`) | `02-intent-file-parser.md` | **Low** — new class, file IO behind a faked abstraction; the one place exceptions are converted to values, so the catch set must be exact | 1 | 3 |
| 3 — Wire intent parsing into `VerifyCommand` + E2E coverage + README schema note | `03-wire-intent-into-verify.md` | **Medium** — changes the runtime path of every `verify` call and the E2E fixture's intent file; the contract's field names become the de-facto public input schema the Stop hook / `CLAUDE.md` rule will target | 2 | — |

**Revert cascade (reverse dependency order):** revert 3, then 2, then 1. Each later phase injects
or constructs a type the earlier one created, so reverting out of order breaks the build.

### Execution waves (parallel subagents)

| Wave | Phases | Runs as |
|---|---|---|
| 1 | 1 | Single subagent on the task branch |
| 2 | 2 | Single subagent on the task branch |
| 3 | 3 | Single subagent on the task branch |

The graph is a **chain**, so the orchestrator is sequential — honestly so: Phase 2 constructs
`IntentResult` / `IntentContract` and injects `IValidateIntent` (all Phase 1 files), and Phase 3
injects `IParseIntent` (Phase 2). Splitting the model types into their own phase to let the validator
and parser run in parallel was considered and rejected: a phase of bare auto-property classes has no
behavior to test-drive, and the parser's tests still need the validator's interface to fake.

## Orchestrator

`orchestrator.md` (this folder) is the runbook. Say **"run intent-contract-model"** — the session runs
Phases 1 → 2 → 3, each in a **fresh general-purpose subagent** (the phase file is the entire
handoff), verifies exactly one new commit and a clean working tree after each, and halts on failure.
It never squashes, amends, rebases, or pushes.

## Decisions (lightweight ADRs)

### ADR-1 — Pinned field names; `IntentContract` + `IntentBehavior` as plain classes
**Decision:** The JSON schema is exactly `{ "why", "class", "responsibility", "behaviors": [ { "behavior", "test" } ] }`.
C# model: `IntentContract { string Why; string Class; string Responsibility; List<IntentBehavior> Behaviors = [] }`
and `IntentBehavior { string Behavior; string Test }` — plain classes with `{ get; set; }` auto-properties,
in `Commands/Verify/Intent/`. `Test` is `null` when omitted.
**Context:** `epics.md` says field names are "finalized in the Intent contract model epic"; the README
example and resolved Open Question use these names. ADR-G4 bans records; collection expressions for
defaults. `Class` is a legal C# property name (only lower-case `class` is a keyword), and
case-insensitive matching maps `"class"` → `Class` with no attribute. The model is named after the
README's own term, "The Intent Contract".
**Alternatives rejected:** `ClassName` + `[JsonPropertyName("class")]` (an attribute to work around a
non-problem); a `*Context` name (`*Context` is the house suffix for mutable working state through one
unit of work — the contract is a declared, read-only spec); records (banned).

### ADR-2 — One `IntentValidationError` enum for the whole intent stage; `IntentResult` mirrors `VerifyArgumentsResult`
**Decision:** A new enum `IntentValidationError { None, IntentFileUnreadable, MalformedIntentJson, MissingWhy, MissingClass, ClassDoesNotMatchProductionFile, MissingResponsibility, NoBehaviors, EmptyBehavior, NonObservableBehavior, InvalidTestName }`
and `IntentResult { bool IsValid; IntentValidationError Error; string Message; IntentContract Intent }`.
Both the parser (read/JSON errors) and the validator (contract errors) return `IntentResult`.
**Context:** ADR-G2 (values not exceptions, enum preferred); item 1's `VerifyArgumentsResult` +
`VerifyUsageError` is the shape the codebase already has for "valid or one tagged error + message" —
matching it keeps `VerifyCommand`'s handling identical for both stages. Usage errors (how the CLI
was invoked) and intent errors (what the AI declared) are different concerns, so they get separate enums.
**Alternatives rejected:** extending `VerifyUsageError` (conflates CLI usage with contract content);
a list of all errors like `RepositoryValidationResult.Errors` (the spine's contract is one error at a
time in fixed precedence — independently assertable, and what the AI fixes next); exceptions (ADR-G2).

### ADR-3 — Observable phrasing enforced by a leading-verb allow-list (user decision 2026-10-14)
**Decision:** A behavior is observable when its first word — after stripping an optional negation
prefix `"Does not "` or `"Never "` — is (case-insensitive) one of:
`Returns, Throws, Calls, Invokes, Logs, Writes, Reads, Saves, Loads, Creates, Adds, Removes, Deletes, Sets, Updates, Sends, Raises, Records, Moves, Reports, Resolves, Executes, Emits, Skips, Stops`.
Otherwise `NonObservableBehavior`, with a message quoting the behavior and suggesting outcome verbs.
The list is a `private static readonly` set inside `ValidateIntent`.
**Context:** README: "Observable phrasing (enforced)… 'Returns X when Y', 'Throws Z when W' — never an
implementation detail ('uses a switch')." The check must be deterministic and microsecond-cheap (sub-10s
budget). The user chose this approach over deferring to the LLM gate or a banned-word list.
**Alternatives rejected:** deferring to the LLM intent gate (README says *enforced*, and the LLM gate
is Full, not MVP); a banned implementation-word list (fuzzy, unbounded, more false negatives). The
list moving to the tool-level config is an Integration-group concern (Open Questions).

### ADR-4 — `class` must equal the production file name (user decision 2026-10-14)
**Decision:** After `MissingClass`, compare `intent.Class.Trim()` ordinally (case-sensitive) to
`Path.GetFileNameWithoutExtension(productionPath)`; a mismatch returns
`ClassDoesNotMatchProductionFile`, naming both.
**Context:** One-class-per-file and file-named-after-class are hard rules
(`naming-and-structure.md`), so the expected class is derivable from the path. This catches the AI
(or the Stop hook's pairing) handing the wrong intent file for a class — cheaply, before any gate.
**Alternatives rejected:** case-insensitive comparison (C# class names are case-sensitive; `foo` ≠
`Foo`); leaving pairing correctness to the Stop hook (the user chose to enforce it here).

### ADR-5 — `ParseIntent` owns the exception→value boundary; it takes the parsed arguments
**Decision:** `IParseIntent.Parse(VerifyArgumentsResult arguments) → Task<IntentResult>` reads
`arguments.IntentPath`, converts **exactly** `IOException` and `UnauthorizedAccessException` into
`IntentFileUnreadable`, treats empty/whitespace content as `IntentFileUnreadable` (FatCat's
`ReadAllText` returns `string.Empty` for a missing file instead of throwing — amended 2026-10-14
after the first Phase 2 attempt; no `FileExists` re-check), and converts `JsonException` into
`MalformedIntentJson`; a deserialized `null` is also `MalformedIntentJson`. Duplicate JSON
properties are allowed (last wins — user decision 2026-10-14). Then it delegates to `IValidateIntent.Validate(contract, arguments.ProductionPath)`.
`VerifyCommand` calls only `IParseIntent`. `JsonOptions` sets `PropertyNameCaseInsensitive = true` and
`UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow`.
**Context:** A malformed or unreadable intent file is a predictable outcome of AI-authored input, so
`System.Text.Json`/IO exceptions must become values (ADR-G2) — at the one place they can occur. Any
other exception is genuinely unexpected and bubbles to `Program.Main`'s boundary. Taking the
`VerifyArgumentsResult` gives the class/file check its production path and is the value item 3's
pipeline will thread anyway. `Disallow` turns a typo like `"tests"` (which would silently drop every
test link) into a visible error.
**Alternatives rejected:** catching `Exception` (hides real faults — banned swallowing);
`IJsonOperations` from the toolkit (its defaults add camelCase/enum converters the contract doesn't
need; the main project uses `JsonSerializer` directly); ignoring unknown members (silent data loss on
a typo); `VerifyCommand` calling parser and validator separately (spreads the intent stage across the
command, which item 3 will turn into a pipeline host).

### ADR-6 — Test link checked for shape only
**Decision:** When `test` is present (non-null) it must match `^[A-Za-z_][A-Za-z0-9_]*$`; an empty,
whitespace, or otherwise non-identifier value is `InvalidTestName`. Omitted (`null`) is valid.
**Context:** README: when named, the tool checks the test "exists and passes" — that needs the
compiled test assembly (Engine/Test gate). Shape is all that is knowable here, and it catches
`"test": "returns X when Y"` prose early.
**Alternatives rejected:** parsing the tests file for the method name now (half-builds the Test gate
with text matching that the Roslyn engine will supersede).

## Assumptions

- **Nullable is disabled** — plain `string`, no `?`, no `null!`. `Test` being `null` means "omitted".
- **`ImplicitUsings` is enabled** — `System.IO`, `System.Linq` are implicit; `System.Text.Json`,
  `System.Text.Json.Serialization`, `System.Text.RegularExpressions`, and `FatCat.Toolkit` need
  explicit `using`s.
- **`JsonUnmappedMemberHandling.Disallow`** exists on `JsonSerializerOptions.UnmappedMemberHandling`
  in .NET 8+ (net10.0 target) and throws `JsonException` on an unknown property.
- **`Faker.Create<string[]>()`** etc. are available as in item 1's tests; tests whose assertions read
  specific fields pin those fields explicitly.
- **The task branch is the current branch `intent-contract-model`** (not `main`) — do not create a
  `task/...` branch.
- **CSharpier uses tabs, printWidth 128** — write readable code; let `dotnet build` format.

## Open Questions

None blocking. Flag for the human reviewer:

- **Exit code still 0 on an invalid intent** — same standing deferral as item 1 ADR-5; item 4 wires
  the non-zero exit.
- **Verb allow-list completeness (ADR-3).** A legitimate behavior starting with an unlisted verb
  (e.g. "Publishes…", "Enqueues…") is rejected until the list grows. Growing it is a one-line change;
  moving it to the tool-level config belongs to the Integration group.
- **Duplicate `test` names across behaviors.** The README maps behaviors 1:1 to tests, so two
  behaviors naming the same test may deserve an error. Not in this item's acceptance criteria; easy
  to add later as `DuplicateTestName` if wanted.
- **README / `CLAUDE.md` intent rule.** Phase 3 updates the README's "schema is close to final" note
  to state the pinned schema. The `CLAUDE.md` intent-file workflow rule is its own Integration item.
