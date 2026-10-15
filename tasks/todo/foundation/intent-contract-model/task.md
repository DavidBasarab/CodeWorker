# Work Item

`intent-contract-model`

# Specification

Turn the **intent file** the AI hands to `verify` into a validated, in-memory **intent contract** —
the working object every later stage of the pipeline consumes. Item 1 (`verify-command-surface`)
already proves the intent file *exists*; this item reads it, deserializes it, and checks it is a
well-formed, checkable contract.

The contract (README "The Intent Contract"; `epics.md` resolved Open Question) — field names are
**pinned by this item**:

```json
{
  "why": "Tasks that exceed the token limit must be re-queued as pending, not failed.",
  "class": "TokenLimitHeuristic",
  "responsibility": "Classify a ProcessResult as TokenLimited when the transcript shows the token-limit signature.",
  "behaviors": [
    { "behavior": "Returns TokenLimited when the transcript contains the limit marker",
      "test": "ReturnTokenLimitedWhenMarkerPresent" },
    { "behavior": "Returns null when no marker is present" }
  ]
}
```

Grounded in `CodeWorker.Cli/README.md`:
- **The intent contract is the spec** the code is judged against, the checklist the tests must
  cover, and the single-responsibility boundary — so it must be complete and unambiguous before any
  gate runs.
- **Observable phrasing (enforced).** Every behavior reads as an observable outcome ("Returns X when
  Y", "Throws Z when W"), never an implementation detail ("uses a switch"). Enforced
  **deterministically** with a leading-verb allow-list (user decision 2026-10-14).
- **Optional test link.** A behavior may name the verb-first `test` method that proves it. This item
  checks only that a named test is a well-formed method identifier; checking the test *exists and
  passes* is the later Test gate.
- **One unit of work = one production class + its test file.** The intent's `class` must match the
  production file's name (`Foo.cs` ⇒ `Foo`) — the one-class-per-file rule makes this deterministic
  and catches the AI pairing the wrong intent file (user decision 2026-10-14).
- **Predictable failures are values, never exceptions** (`epics.md`; ADR-G2;
  `.claude/rules/csharp/errors-and-logging.md`) — an unreadable file, malformed JSON, or an invalid
  contract each return a distinct, tagged error value.

## Acceptance Criteria

- [ ] A valid intent JSON file produces a **valid result carrying a populated `IntentContract`**
      (`Why`, `Class`, `Responsibility`, and every behavior with its optional `Test`).
- [ ] JSON property names are matched case-insensitively; a property the contract does not define
      (e.g. a misspelled `"behaviours"` or `"tests"`) is rejected as malformed rather than silently
      dropped.
- [ ] An intent file that cannot be read returns `IntentFileUnreadable` — no exception escapes.
- [ ] Malformed JSON, a JSON `null`, or a non-object payload returns `MalformedIntentJson` — no
      exception escapes.
- [ ] Each invalid contract returns a **distinct `IntentValidationError`** naming what is wrong:
      `MissingWhy`, `MissingClass`, `ClassDoesNotMatchProductionFile`, `MissingResponsibility`,
      `NoBehaviors`, `EmptyBehavior`, `NonObservableBehavior`, `InvalidTestName`.
- [ ] Every failure carries a specific human-readable `Message` (naming the field, and for behavior
      errors the 1-based behavior number and its text).
- [ ] Errors are returned **one at a time in a fixed precedence** (read → JSON → why → class →
      class/file match → responsibility → behaviors in order), so each case is independently
      assertable.
- [ ] `verify` with valid arguments reads and validates the intent: an invalid intent logs its
      specific error at `Error`; a valid intent logs the contract's class and behavior count.
- [ ] Unit-tested against sample intent JSON (round-trip into `IntentContract`) with a faked
      `IFileSystemTools`; the E2E suite covers a valid intent and at least one invalid intent through
      the published CLI; zero warnings; `dotnet test CodeWorker.sln` green.

## Out of Scope / Non-Goals

- **Checking that a named `test` exists or passes** — the Test gate (Deterministic gates group).
  This item only checks the name is a well-formed identifier.
- **Judging whether the code does what the behaviors declare** — the LLM intent gate (Full).
- **Running gates / a pipeline** — item 3 (`gate-pipeline-framework`). This item produces the
  validated contract that item 3 will consume.
- **Process exit codes and the JSON verdict** — item 4 (`verdict-model-reporting`). An invalid intent
  still exits 0 until then (same standing deferral as item 1's ADR-5).
- **Configurable verb list** — the observable-verb allow-list is a constant here; moving it to the
  tool-level config is an Integration-group concern.
- **Multiple intents / classes per invocation** — one invocation is one unit of work (item 1 ADR-3).
- **Duplicate-test-name detection across behaviors** — see overview Open Questions.

## Feature Context

- Epic plan: [`tasks/todo/foundation/00-epic-plan.md`](../00-epic-plan.md) — item 2, and the group
  ADRs (ADR-G2 values-not-exceptions; ADR-G3 `Commands/Verify/Intent/`; ADR-G4 plain classes +
  `System.Text.Json`; ADR-G5 capability interfaces + scanning).
- Previous item (built): [`tasks/done/foundation/verify-command-surface/`](../../../done/foundation/verify-command-surface/00-overview.md)
  — `VerifyArgumentsResult`, `ParseVerifyArguments`, `VerifyCommand`.
- Source of truth: [`CodeWorker.Cli/README.md`](../../../../CodeWorker.Cli/README.md) — "The Intent
  Contract".
- House JSON pattern: `CodeWorker/Commands/Run/LoadRepoSettings.cs` (`IFileSystemTools.ReadAllText`
  + `JsonSerializer.Deserialize` with a `static readonly JsonSerializerOptions`).

# Notes

- `IFileSystemTools` (FatCat.Toolkit, `C:\Code\FatCat.Toolkit\src\ToolKit\FileSystemTools.cs`)
  exposes `Task<string> ReadAllText(string path)`; it auto-registers via `SystemScope` scanning.
- Decisions made with the user on 2026-10-14: leading-verb allow-list for observable phrasing; the
  intent `class` must match the production file name; this session writes the plan only.
