# verify-command-surface — Overview

- **Work item:** `verify-command-surface` (Foundation item 1 — see
  [`../00-epic-plan.md`](../00-epic-plan.md))
- **Source of truth:** `CodeWorker.Cli/README.md`
- **Generated:** 2026-09-02-130729

## Work Item

Stand up the CLI's **first command surface**: a `verify` command reached through a real
`ICommand` / `IResolveCommand` dispatch, whose only job (for now) is to parse and validate the
three file-path flags — `--intent`, `--production`, `--tests` — into a **parsed-arguments value**
that later Foundation items consume. Every predictable failure (missing flag, missing file, no
arguments) is a **returned value, never an exception**.

### Current state that shapes the design (verified against the code)

- **`Program.Main`** (`CodeWorker.Cli/Program.cs`) calls
  `SystemScope.Initialize(new ContainerBuilder(), [typeof(Program).Assembly, typeof(ConsoleLog).Assembly], ScopeOptions.SetLifetimeScope)`,
  resolves `CodeWorkerCliApplication`, and `await`s `Run(args)`. It wraps everything in a
  try/catch that calls `ConsoleLog.WriteException(ex)` — the one boundary that turns an unexpected
  fault into console output. **Do not add exit-code wiring here** (item 4 owns that).
- **`CodeWorkerCliApplication.Run`** (`CodeWorker.Cli/CodeWorkerCliApplication.cs`) logs
  `"Welcome to Code Worker CLI"` then `await processArguments.Process(args)`. Leave this class
  unchanged — the dispatch grows inside `ProcessArguments`.
- **`ProcessArguments.Process`** (`CodeWorker.Cli/Commands/ProcessArguments.cs`) is a **stub**: it
  logs `"No arguments provided"` on empty args, otherwise logs the argument count, and
  `await Task.CompletedTask`. There is **no `ICommand`, no `CommandResolver`** in the CLI yet.
  This item replaces the stub body with real resolve-and-execute dispatch.
- **`CodeWorkerCliModule`** registers only a Serilog console `ILogger`. Everything else is resolved
  by `SystemScope` assembly scanning (single-implementation interfaces auto-register). **No module
  change is needed for this item** (see ADR-4).
- **`IFileSystemTools`** (FatCat.Toolkit) auto-resolves: `SystemScope.Initialize` force-includes
  `typeof(SystemScope).Assembly` (the `FatCat.Toolkit` assembly) and `typeof(IFileSystem).Assembly`
  via `EnsureAssembly`, and `RegisterAssemblyTypes(...).AsImplementedInterfaces()` registers the
  single-impl `IFileSystemTools`. The main `CodeWorker` project injects it with the identical
  bootstrap — verified in `C:\Code\FatCat.Toolkit\src\ToolKit\...\SystemScope.cs`. `FileExists` is
  synchronous.
- **The pattern to mirror** is the main project: `CodeWorker/Commands/ICommand.cs`
  (`Task Execute(string[] args)`), `CodeWorker/Commands/CommandResolver.cs` (`IResolveCommand` +
  a `switch` on `args[0].ToLowerInvariant()` with a default arm), and
  `CodeWorker/Commands/Info/InfoCommand.cs` (a capability interface `IRunInfoCommand : ICommand`
  declared immediately above the command class, in the same file).
- **Test stack** (`CodeWorker.Cli.Tests/GlobalUsings.cs`): xUnit, FakeItEasy (`A.Fake<T>`,
  `A.CallTo`), FluentAssertions, `FatCat.Fakes` (`Faker.Create<T>`). Tests mirror source folders
  with the `Testing.` prefix. `ProcessArgumentsTests` currently asserts the stub logging and **will
  be rewritten** in Phase 1 (its old behavior is deliberately removed).

## Acceptance Criteria → Phase Map

| Acceptance criterion (task.md) | Proven by |
|---|---|
| `verify` resolves through a CLI `IResolveCommand`/`CommandResolver` switch dispatch | Phase 1 (resolver + `ProcessArguments` rewire + `VerifyCommand` skeleton) |
| Well-formed invocation yields a valid parsed-arguments value with the three paths | Phase 2 (parser success path) + Phase 3 (command surfaces it) |
| Missing flag → specific usage-error value (no exception) | Phase 2 (`MissingIntentFlag`/`MissingProductionFlag`/`MissingTestsFlag`) + Phase 3 (command logs it) |
| Missing file → specific usage-error value via faked `IFileSystemTools` | Phase 2 (`IntentFileNotFound`/`ProductionFileNotFound`/`TestsFileNotFound`) |
| No arguments → usage-error value | Phase 2 (`NoArguments`) + Phase 3 (end-to-end smoke) |
| Each error is a distinct, independently assertable value | Phase 2 (`VerifyUsageError` enum + `VerifyArgumentsResult`, one test per case) |
| Unit-tested via faked file system + CLI dispatch; zero warnings; `dotnet test` green | Every phase's Definition of Done + Phase 3 end-to-end |

## Phases & Dependency Graph

| Phase | File | Risk | Depends on | Depended on by |
|---|---|---|---|---|
| 1 — Command-pattern seam + `verify` skeleton (`ICommand`, `IResolveCommand`/`CommandResolver`, `IRunVerifyCommand`/`VerifyCommand` shell, `ProcessArguments` rewired to dispatch) | `01-command-seam.md` | **Medium** — rewires the single dispatch path every CLI invocation flows through; replaces the `ProcessArguments` stub behavior (and its tests). No public/network/data surface. | — | 3 |
| 2 — Verify argument model + parser (`VerifyUsageError`, `VerifyArgumentsResult`, `IParseVerifyArguments`/`ParseVerifyArguments` with flag parsing + `IFileSystemTools.FileExists`) | `02-argument-parser.md` | **Low** — a pure, isolated value + parser; not yet wired into the command | — | 3 |
| 3 — Wire the parser into `VerifyCommand` + usage reporting + end-to-end CLI smoke | `03-wire-verify-command.md` | **Low** — connects two already-tested pieces; logs the usage error | 1, 2 | — |

**Revert cascade (reverse dependency order):** revert 3 first, then 1 and 2 in either order.
Phases 1 and 2 are independent (2 does not touch anything 1 creates), so either can be reverted
alone once 3 is reverted. Reverting 1 or 2 while 3 stands would break the build — revert 3 first.

## Orchestrator

`orchestrator.md` (this folder) is the runbook. Usage: open a Claude session in this repo and say
**"run verify-command-surface"** — the session drives the phases in dependency order (1 → 2 → 3),
each in a **fresh, isolated context** (one general-purpose subagent per phase; the phase file is
the entire handoff), verifying exactly one new commit and a clean working tree after each phase,
halting on failure. It never squashes, amends, rebases, or pushes.

## Decisions (lightweight ADRs)

### ADR-1 — Full `ICommand` + `CommandResolver` dispatch in the CLI, driven from `ProcessArguments`
**Decision:** Introduce the command pattern into the CLI now, mirroring the main project:
`Commands/ICommand.cs` (`Task Execute(string[] args)`), `Commands/CommandResolver.cs`
(`IResolveCommand` with a `switch` on `args[0].ToLowerInvariant()`), and a capability interface
`IRunVerifyCommand : ICommand`. `ProcessArguments.Process` becomes the dispatch: it injects
`IResolveCommand`, resolves the command, and executes it — nothing more.
**Context:** This is the group-level **ADR-G1**, resolved by the user (2026-09-02): build the full
resolver now because more commands are planned, so standing it up here removes the risk and cost of
retrofitting one around a bespoke branch when the second command lands. `naming-and-structure.md`
mandates switch-expression dispatch; the README calls `verify` "the first of several commands."
**Alternatives rejected:** a bespoke `if`/`else` inside `ProcessArguments` (violates the
switch-expression + command-pattern rules; doesn't scale); copying the whole main-project resolver
verbatim including its `run-task` default (that default is a main-project concept — the CLI's
default is the only command it has).

### ADR-2 — Unknown/empty verbs resolve to the verify command (the only command); the parser owns all usage errors
**Decision:** `CommandResolver.Resolve` returns the verify command for `"verify"` and for the
default arm (`_`), and for empty args. All usage validation — missing flags, missing files, no
arguments — lives in **one place**, the verify argument parser (Phase 2), which returns a typed
`VerifyArgumentsResult`. The resolver stays a dumb lookup.
**Context:** The switch needs a default arm (`naming-and-structure.md`), throwing on a predictable
input is banned (`errors-and-logging.md`, ADR-G2), and there is no `help` command to route to yet.
The main project uses the same "unknown routes to the primary command" convention
(`_ => runTaskCommand`). With one command, both the named arm and the default return the verify
command; that duplicated arm is the **deliberate extension seam** where the second command drops in
— not dead code to "simplify" away (a note in Phase 1's hand-off tells the reviewer this).
**Alternatives rejected:** inventing an `UnknownCommand`/`HelpCommand` now (out of scope — no
Foundation item asks for it; adds an unreachable type); the resolver returning a nullable/So the
caller must null-check (spreads usage handling across layers — keep it in the parser).

### ADR-3 — One unit of work per invocation: single intent, single production, single tests
**Decision:** `verify` accepts exactly one `--intent`, one `--production`, and one `--tests` value.
`VerifyArgumentsResult` carries three `string` paths, not collections.
**Context:** The README defines a unit of work as "one production class plus its unit test file"
and shows singular flags in Usage. Pairing many changed classes is explicitly the Stop hook's job
(Integration group), which calls `verify` once per class. Modeling collections now would be
speculative generality against the README.
**Alternatives rejected:** `string[]` per flag (over-engineering vs. the README's unit-of-work
definition; the multi-file concern belongs to the hook layer). Flagged as an Open Question so a
later change is a conscious decision, not a silent assumption.

### ADR-4 — No `CodeWorkerCliModule` change; rely on `SystemScope` scanning
**Decision:** Register nothing new in `CodeWorkerCliModule`. `ICommand`/`IResolveCommand`/
`IRunVerifyCommand`/`IParseVerifyArguments` each have a single implementation and auto-register via
`SystemScope`'s `RegisterAssemblyTypes(...).AsImplementedInterfaces()`. `IFileSystemTools` is
force-scanned from the `FatCat.Toolkit` assembly by `EnsureAssembly(typeof(SystemScope).Assembly)`.
**Context:** `types-and-di.md` — register in the module only for multiple implementations / chosen
instances / lifetimes scanning can't infer. None of this item's types qualify. The main project
injects `IFileSystemTools` with the identical bootstrap and no explicit registration.
**Alternatives rejected:** registering each type explicitly (fights the scanning convention; noise);
adding `IFileSystemTools` to the module (already resolvable — a redundant registration).

### ADR-5 — The parsed-arguments value carries the failure *intent*; process exit stays 0 until item 4
**Decision:** A malformed invocation produces a `VerifyArgumentsResult` with `IsValid == false`, a
tagged `VerifyUsageError`, and a human-readable `Message` that `VerifyCommand` logs at `Error`.
This item does **not** set `Environment.ExitCode` or return an exit code from `Main`.
**Context:** `epics.md` splits concerns — item 1 owns the parsed-arguments *value* ("a value, not
an exception"); item 4 (Verdict model + reporting) owns "exit code 0 on green, non-zero on any
block" and the return channel from command to process. Wiring an exit code here would pre-build item
4 and create a second place that sets it.
**Alternatives rejected:** setting `Environment.ExitCode = 1` on usage error now (duplicates item
4's responsibility; two code paths mutating exit state). Recorded as an Open Question so the
temporary "logs an error but exits 0" behavior is visible to the reviewer.

## Assumptions

- **Nullable is disabled** (`<Nullable>disable</Nullable>`) — declare reference types plainly, no
  `?` annotations, no `null!`. `VerifyArgumentsResult` string paths are plain `string`.
- **`ImplicitUsings` is enabled** in the CLI project — no `GlobalUsings.cs` in production; the test
  project's `GlobalUsings.cs` already covers the test stack. `IFileSystemTools` needs
  `using FatCat.Toolkit;` where referenced.
- **`IFileSystemTools.FileExists(string)` is synchronous and returns `bool`** (verified against
  main-project call sites, e.g. `ClaudeRunner`, `RunSingleTaskCommand`). The parser is therefore
  synchronous; `VerifyCommand.Execute` returns `Task.CompletedTask` (block body, no `async`
  keyword) to avoid a CS1998 "async method lacks await" warning under the zero-warning gate.
- **The task branch is the current branch `CliTester`** (not `main`), so per the commit policy work
  happens on it — do **not** create `task/verify-command-surface`.
- **CSharpier uses tabs** (`.csharpierrc`, `useTabs: true`, `printWidth: 128`) — write readable
  code and let CSharpier format on build; do not hand-format.

## Open Questions

None blocking. Flag for the human reviewer:

- **Exit code deferred (ADR-5):** after this item a malformed `verify` invocation logs a clear
  usage error but the process still exits 0. Item 4 (Verdict model + reporting) wires the non-zero
  exit. If you want a non-zero exit sooner, that is a small addition to item 4's scope, not a
  reopening of this item.
- **Unknown-verb specificity (ADR-2):** with only one command, a non-`verify` first token routes to
  verify and surfaces verify's flag usage (e.g. "missing --intent") rather than a bespoke "unknown
  command 'foo'" message. A dedicated help/usage command becomes worthwhile when the second command
  lands and there is a real verb table to report against. Not a Foundation item today.
- **Single vs. multiple files (ADR-3):** one production + one test file per invocation matches the
  README's unit-of-work definition; multi-file batching is the Stop hook's job. If a real need for
  multiple production files per call appears, revisit the result shape then.
