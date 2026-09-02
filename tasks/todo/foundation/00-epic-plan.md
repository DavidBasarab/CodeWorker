# Foundation — Epic Plan

- **Epic (group):** `### Foundation (the pipeline spine)` in `epics.md` (MVP)
- **Source of truth:** `CodeWorker.Cli/README.md` (what the CLI is and does)
- **Generated:** 2026-08-31-213545
- **Workflow:** plan one item → build it → adjust → plan the next. Items below that are not yet
  planned stay as one-liners on purpose (see "Ordering rule"). Nothing here writes production
  code — expanding an item (Mode B) writes its `task.md` + phased plan; building it is a separate,
  human-driven run.

## What Foundation is

The five items below are the **pipeline spine** of the `verify` command: the parts that take the
AI's submission (intent + file paths), run it through an ordered set of gates, and return a
verdict. Foundation builds the *skeleton the gates plug into* — it deliberately contains **no gate
logic and no Roslyn engine** (those are the separate `Engine` and `Deterministic gates` groups).
When Foundation is done, `verify` parses a real submission, runs an (initially empty) fail-fast
gate pipeline, and emits a real verdict + exit code — the frame every later group hangs on.

## Current state that shapes the design (verified against the code)

- `CodeWorker.Cli` today is a bare scaffold: `Program.Main` calls `SystemScope.Initialize(...)`
  over `[typeof(Program).Assembly, typeof(ConsoleLog).Assembly]`, resolves
  `CodeWorkerCliApplication`, and calls `Run(args)`. `Run` logs a welcome line and delegates to
  `IProcessArguments.Process(args)`.
- `ProcessArguments.Process` (`CodeWorker.Cli/Commands/ProcessArguments.cs`) is a **stub**: it
  logs "No arguments provided" on empty args, otherwise logs the argument count, and awaits
  `Task.CompletedTask`. There is **no command pattern, no `ICommand`, no `CommandResolver`** in the
  CLI yet (unlike the main `CodeWorker` project, which has both).
- `CodeWorkerCliModule` registers only a Serilog console logger (`ILogger`). Everything else is
  resolved by `SystemScope` assembly scanning (single-implementation interfaces auto-register).
- `CodeWorker.Cli.Tests` mirrors the CLI project one-for-one (per `CLAUDE.md` repository layout).

So Foundation introduces the CLI's **first real command infrastructure**. The `verify` command
hangs off the existing `IProcessArguments` dispatch (per the epic text), following the command
pattern the README and `.claude/rules/csharp/naming-and-structure.md` describe.

## Items (dependency order)

### 1. Verify command surface — `verify-command-surface`
- **Scope:** Add a `verify` command to `CodeWorker.Cli` on the existing `IProcessArguments`
  dispatch. It parses the intent file path, production file path(s), and test file path(s) from
  args (`verify --intent intent.json --production Foo.cs --tests FooTests.cs`, per the README
  Usage). Invalid input (missing flags, unknown verb, no such file) returns a **clear usage error
  as a value — never an exception** (`epics.md`; `.claude/rules/csharp/errors-and-logging.md`).
  This is the entry point; it produces the parsed, validated arguments the rest of the spine
  consumes. It does not yet parse the intent *content* (item 2) or run gates (item 3).
- **Depends on:** — (first item; builds on the existing `ProcessArguments` dispatch)
- **Depended on by:** 2
- **Acceptance shape:** `verify` with valid flags resolves to the verify command and yields a
  parsed-arguments value carrying the three paths; each malformed invocation returns a specific,
  human-readable usage error value with a non-zero exit intent; unit-tested via faked file-system
  and the CLI dispatch.
- **Plan:** not yet planned  <!-- becomes: tasks/todo/foundation/verify-command-surface/ -->

### 2. Intent contract model — `intent-contract-model`
- **Scope:** Parse and validate the structured intent payload the AI submits —
  `{ why, class, responsibility, behaviors[] }`, where each behavior is `{ behavior, test? }`
  (README "The Intent Contract"; `epics.md` resolved Open Question) — into a working-context object
  the pipeline consumes. A missing/unreadable intent file or a malformed/invalid payload
  (empty `class`, no behaviors, non-observable phrasing where enforced) is a **returned error, not
  an exception**. Deserialization is `System.Text.Json` (house style).
- **Depends on:** 1 (the command surface supplies the validated intent file path this parses)
- **Depended on by:** 3
- **Acceptance shape:** a valid intent JSON produces a populated intent-context object with all
  behaviors; each invalid payload class returns a distinct validation error value naming what is
  wrong; round-trips are unit-tested against sample intent JSON.
- **Plan:** not yet planned  <!-- becomes: tasks/todo/foundation/intent-contract-model/ -->

### 3. Gate pipeline framework — `gate-pipeline-framework`
- **Scope:** The `IVerificationGate` abstraction and the **fail-fast runner** (README "How It
  Works"): an ordered list of gates, short-circuit on the first blocking failure, and a per-gate
  result carrying pass/fail, locations, reasons, and timing. Foundation ships the framework with
  **zero concrete gates** — later groups add compile/test/mutation/standards/intent. The runner
  consumes the intent context (item 2) and the parsed arguments (item 1).
- **Depends on:** 2
- **Depended on by:** 4, 5
- **Acceptance shape:** given an ordered set of fake gates, the runner runs them cheapest-first,
  stops at the first blocking failure (later gates never run), and returns each gate's structured
  result with timing; an all-pass run returns all results; unit-tested with faked `IVerificationGate`s.
- **Plan:** not yet planned  <!-- becomes: tasks/todo/foundation/gate-pipeline-framework/ -->

### 4. Verdict model + reporting — `verdict-model-reporting`
- **Scope:** Aggregate the gate results (item 3) into one **verdict**: a machine-readable JSON
  verdict for the `Stop` hook to feed back to the AI, and a human-readable report for the morning
  review (README "Usage"). Exit code is **0 on green, non-zero on any block**. This closes the
  spine: `verify` now returns a real verdict and process exit code.
- **Depends on:** 3
- **Depended on by:** — (the Engine and gate groups plug results into this)
- **Acceptance shape:** a green result set serializes to a green JSON verdict + a clean report and
  exit 0; a set with any blocking failure serializes to a red verdict naming the gate/location/
  reason + a red report and a non-zero exit; both outputs are unit-tested against known result sets.
- **Plan:** not yet planned  <!-- becomes: tasks/todo/foundation/verdict-model-reporting/ -->

### 5. Language engine seam — `language-engine-seam`
- **Scope:** Per-language engine resolution behind the gates: a capability interface
  (`IResolveLanguageEngine`) that hands a gate the right engine for a unit of work, with the C#
  engine registered as the default. Gates never hard-code a language (README "C#-first,
  language-agnostic by design"). Foundation ships the **seam and its registration point only** —
  the real in-process Roslyn engine is the separate `Engine` group; a placeholder/no-op engine may
  back the seam until then, and a second language is deferred (Full).
- **Depends on:** 3 (gates resolve their engine through this seam)
- **Depended on by:** — (the C# `Engine` group registers the real engine into this seam)
- **Acceptance shape:** resolving an engine for a C# unit of work returns the registered C# engine;
  an unsupported language returns a clear "no engine" error value (not an exception); the seam is
  fakeable and unit-tested; swapping/adding an engine needs no gate change.
- **Plan:** not yet planned  <!-- becomes: tasks/todo/foundation/language-engine-seam/ -->

## Group decisions (lightweight ADRs)

### ADR-G1 — `verify` hangs off `IProcessArguments`, adopting the CLI's first command-pattern dispatch
**Decision:** Item 1 introduces the command pattern into the CLI as the README describes
(`args[0]` selects an `ICommand`; a `verify` capability interface extends `ICommand`), with the
existing `IProcessArguments.Process` as the dispatch that resolves and runs it. This mirrors the
main `CodeWorker` project's `ICommand` / `CommandResolver` switch-expression pattern rather than
inventing a CLI-specific one.
**Context:** `epics.md` says "add a `verify` command … on the existing `IProcessArguments`
dispatch"; the README says the CLI uses the command pattern and `verify` is "the first of several
commands." The CLI has no `ICommand`/`CommandResolver` today, so Foundation is where that
infrastructure is born — kept minimal (one command) but shaped like the main project's so the next
command drops in cleanly.
**Alternatives rejected:** a bespoke if/else in `ProcessArguments` (violates the switch-expression
and command-pattern rules; doesn't scale to "several commands"); ripping out `IProcessArguments`
and copying `CommandResolver` wholesale now (over-engineering for one command — grow it when the
second command lands). **Flagged as an open question** — this materially shapes item 1's design;
confirm before item 1 is planned in Mode B.

### ADR-G2 — Predictable failures are returned values, never exceptions
**Decision:** Every foreseeable failure in the spine — bad args, missing/unreadable/invalid intent,
unsupported language, a blocking gate — is expressed as a **returned value** (an enum or a small
result type carrying the reason), not a thrown exception. Exceptions stay reserved for genuinely
unexpected faults.
**Context:** `epics.md` items 1–2 say "a value, not an exception" / "a returned error";
`errors-and-logging.md` bans exceptions for predictable outcomes and prefers an enum. This is the
verdict philosophy end-to-end: a red gate is data, not a crash.
**Alternatives rejected:** throwing `ArgumentException`/`FileNotFoundException` for user-facing
input problems (turns an expected outcome into an exception; the README's non-zero-exit-with-reason
contract wants a value).

### ADR-G3 — Verify types live under `Commands/Verify/`, namespace mirrors folder
**Decision:** All Foundation types live under `CodeWorker.Cli/Commands/Verify/` with sub-folders
per concern: the command in `Commands/Verify/`, the intent model in `Commands/Verify/Intent/`, the
pipeline in `Commands/Verify/Gates/`, the verdict in `Commands/Verify/Verdict/`, the engine seam in
`Commands/Verify/Engines/`. Namespaces match folder paths exactly
(`FatCat.CodeWorker.Cli.Commands.Verify`, `…Verify.Intent`, `…Verify.Gates`, `…Verify.Verdict`,
`…Verify.Engines`). Tests mirror in `CodeWorker.Cli.Tests` with the `Testing.` prefix.
**Context:** `naming-and-structure.md` — one sub-folder per feature under `Commands/`, namespace
matches folder, test project mirrors source. This is the main project's own layout
(`Commands/Run/…`) applied to the CLI.
**Alternatives rejected:** a flat `Verify/` at project root (breaks the `Commands/`-per-verb
convention); scattering intent/verdict types across ad-hoc folders (namespace/folder drift).

### ADR-G4 — Contracts are plain classes + `System.Text.Json`; collection expressions throughout
**Decision:** The intent context, per-gate result, and verdict are **plain classes** (records are
banned) using auto-properties and collection expressions (`[]`). JSON in/out (intent parse, verdict
emit) uses `System.Text.Json`, matching the README's stated config/house style.
**Context:** `types-and-di.md` (records banned, collection expressions, classes only); README says
the CLI config is `System.Text.Json`. Keeping serialization on one library avoids a second JSON
dependency.
**Alternatives rejected:** records (banned); `Newtonsoft.Json` (not the house library);
`List<T>()`/`new T[0]` initializers (banned in favor of `[]`).

### ADR-G5 — Capability interfaces + `SystemScope` scanning; the module registers only the many-impl seams
**Decision:** Each concern is a narrow, capability-named interface (`IParseVerifyArguments`,
`IParseIntent`, `IRunGatePipeline`, `IBuildVerdict`, `IResolveLanguageEngine`) with constructor
injection, auto-registered by `SystemScope` scanning. `CodeWorkerCliModule` gains registrations
**only** where scanning cannot choose — i.e. the **multiple `IVerificationGate` implementations**
(later groups) and the **language engines** behind the seam (item 5).
**Context:** `types-and-di.md` — register in the module only for multiple implementations / chosen
instances / lifetimes scanning can't infer; otherwise let scanning resolve single-impl interfaces.
`IVerificationGate` and the engines are the two genuine multi-impl points in the spine.
**Alternatives rejected:** registering every type explicitly in the module (fights the scanning
convention; noise); a service-locator pull from `SystemScope` inside the runner (banned — inject
the gates/engine resolver).

## Ordering rule

Detailed phased plans are written **one item at a time** (Mode B), only when the item is next.
Reverting or re-planning a built item never strands a downstream plan, because downstream items
are not planned until reached. If, after building item 1, the command-surface shape changes the
design for items 2–5, only item 1's plan exists to reconcile — the rest are still one-liners here,
edited freely.

## Next item

**`verify-command-surface`** — say **"plan verify command surface"** (or "plan the next foundation
item") to expand it into its `task.md` + phased plan. **Resolve ADR-G1 first** (command-pattern
dispatch shape) — it materially shapes that item.

## Assumptions & open questions

- **[Open — resolve before item 1] ADR-G1 dispatch shape.** Confirm that item 1 introduces the
  `ICommand` + resolver pattern into the CLI (mirroring the main project) rather than branching
  inside `ProcessArguments`. This changes item 1's file/interface layout.
- **The CLI has no command infrastructure yet.** Item 1 is the first to add it; there is no
  existing CLI `ICommand`/`CommandResolver`/`CommandModule` to extend — only `IProcessArguments`.
- **Engine seam ships without a real engine.** Item 5 delivers the seam + registration point; the
  in-process Roslyn C# engine is the separate `Engine` group. A placeholder/no-op engine likely
  backs the seam until then — confirm that's acceptable, or fold the first real engine registration
  into the `Engine` group and have item 5 register nothing concrete.
- **Intent schema field names.** Treated as `{ why, class, responsibility, behaviors[] of
  { behavior, test? } }` per the resolved Open Question; exact names are finalized *in* item 2
  (`epics.md` note). Item 1 only needs the file *path*, so it is unaffected by the final names.
- **Verdict JSON shape** (the `Stop`-hook contract) is defined in item 4; the hook-wiring epic
  (Integration group) consumes it later. Item 4 owns the field names; pin them there.
- **Sub-10s budget does not bite in Foundation.** The spine has no Roslyn/LLM work; the budget
  applies once the Engine and gate groups land. Foundation just must not design anything that
  forces per-call `dotnet build`/`dotnet test` later (it doesn't — gates are an abstraction here).
