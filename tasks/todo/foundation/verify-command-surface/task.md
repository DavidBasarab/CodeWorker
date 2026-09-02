# Work Item

`verify-command-surface`

# Specification

Give the CodeWorker CLI its **first real command surface**: a `verify` command that Claude (or a
person) invokes as

```
verify --intent intent.json --production Foo.cs --tests FooTests.cs
```

This item builds only the **entry point** of the verification spine — the part that recognizes the
`verify` verb, parses the three file-path flags, validates them, and produces a **parsed-arguments
value** the rest of the pipeline (later Foundation items) will consume. It follows the command
pattern the README and `.claude/rules/csharp/naming-and-structure.md` describe, mirroring the main
`CodeWorker` project's `ICommand` / `CommandResolver` dispatch.

Grounded in `CodeWorker.Cli/README.md`:
- **Usage** — `verify --intent intent.json --production Foo.cs --tests FooTests.cs`; the tool is
  handed the intent file, the production file, and the test file (README "Usage", "What It Does").
- **A unit of work is one production class plus its unit test file** (README "What It Does") — so
  one `verify` invocation names exactly one intent, one production file, and one test file.
- **Predictable failures are values, never exceptions** — bad flags or a missing file must return a
  clear usage error, not throw (`epics.md` item 1; `.claude/rules/csharp/errors-and-logging.md`).

What this item does **not** do (owned by later Foundation items, out of scope below): parse the
*contents* of the intent file (item 2 — Intent contract model), run any gate (item 3 — Gate pipeline
framework), or set the real process exit code / emit a verdict (item 4 — Verdict model + reporting).

## Acceptance Criteria

- [ ] Invoking the CLI with `verify` resolves to the verify command through a CLI
      `IResolveCommand` / `CommandResolver` switch-expression dispatch (the CLI's first
      command-pattern infrastructure), mirroring the main project.
- [ ] A well-formed invocation (`verify --intent <i> --production <p> --tests <t>` where all three
      files exist) yields a **valid parsed-arguments value** carrying the three resolved paths.
- [ ] A missing `--intent`, `--production`, or `--tests` flag (or a flag with no value) returns a
      **specific, human-readable usage error value** naming which flag is missing — no exception.
- [ ] A flag whose file does not exist returns a **specific usage error value** naming which file
      (intent / production / tests) was not found — no exception. File existence is checked through
      the faked-in-tests `IFileSystemTools` abstraction.
- [ ] An invocation with no arguments returns a usage error value (not an exception, not a crash).
- [ ] Every error case is a distinct, independently assertable value (an enum-tagged result), so a
      failing test names exactly which malformed invocation broke.
- [ ] All behavior is unit-tested via a faked `IFileSystemTools` and the CLI dispatch; the whole
      solution builds with zero warnings and `dotnet test` is green.

## Out of Scope / Non-Goals

- **Parsing the intent JSON payload** (`why`/`class`/`responsibility`/`behaviors[]`) — that is item
  2 (Intent contract model). This item validates only that the intent file *path exists*.
- **Running any gate** or building a pipeline — item 3.
- **Setting the process exit code or emitting a verdict/report** — item 4. This item produces the
  parsed-arguments *value* (which carries the failure intent); translating that to a non-zero
  process exit is item 4's job. Until then the process still exits 0.
- **Multiple production/test files per invocation** — one `verify` call is one unit of work = one
  production file + one test file + one intent (README). Batching many changed classes is the Stop
  hook's job (Integration group), not the command surface's.
- **A `help` / usage command or a multi-command "unknown command X" table** — there is only one
  command today; a dedicated help surface becomes meaningful when the second command lands. See the
  overview's Open Questions.
- **Config-file loading** (`VerifySettings`) — Integration group.

## Feature Context

- Epic plan: [`tasks/todo/foundation/00-epic-plan.md`](../00-epic-plan.md) — item 1, and the
  group ADRs (ADR-G1 dispatch shape **resolved: full resolver pattern**; ADR-G2 values-not-exceptions;
  ADR-G3 folder layout; ADR-G4 plain classes; ADR-G5 capability interfaces + scanning).
- Source of truth: [`CodeWorker.Cli/README.md`](../../../../CodeWorker.Cli/README.md).
- Pattern to mirror: `CodeWorker/Commands/ICommand.cs`, `CodeWorker/Commands/CommandResolver.cs`,
  `CodeWorker/Commands/Info/InfoCommand.cs` (capability interface + command in one file).

# Notes

- The FatCat toolkit source is at `C:\Code\FatCat.Toolkit\src`. `IFileSystemTools` lives in the
  `FatCat.Toolkit` assembly and auto-registers — `SystemScope.Initialize` force-includes that
  assembly via `EnsureAssembly(typeof(SystemScope).Assembly)`, so no module registration is needed
  (verified against `SystemScope.cs` and the main project, which injects `IFileSystemTools` with the
  identical bootstrap).
