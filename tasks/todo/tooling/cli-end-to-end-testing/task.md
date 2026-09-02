# Work Item

`cli-end-to-end-testing`

# Specification

Give the CodeWorker CLI its **first end-to-end (black-box) test suite**: a test project that
**publishes the real CLI executable** and **invokes it as a subprocess**, then asserts on what a user
(or Claude) would actually observe — the process's **standard output** and **exit code** — for the
`verify` command surface that exists today.

Every existing test in `CodeWorker.Cli.Tests` is a **unit** test: it constructs a class directly and
fakes its collaborators (`IFileSystemTools`, `IResolveCommand`, `ILogger`). None of them prove that
the wired-up, published `FatCatCodeWorkerCli.exe` actually runs, dispatches to the verify command,
parses its flags, and prints the messages the code intends. This item closes that gap with a genuine
outside-in harness — the same way Claude will call the installed CLI from a known directory (README:
"Configured From the Outside", "How Claude actually hits it").

Grounded in `CodeWorker.Cli/README.md` and the current code:

- **Usage** — `verify --intent intent.json --production Foo.cs --tests FooTests.cs` (README "Usage").
  The E2E suite runs exactly this shape against the compiled exe.
- **The observable contract today is stdout.** The verify command surface (built by
  `verify-command-surface`, phases 1–3, already committed) parses the three flags and **logs** the
  outcome through Serilog's console sink:
  - valid → `logger.Information("verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}", …)`
  - malformed → `logger.Error("verify: {Reason}", result.Message)` where `Reason` is the parser's
    usage message (e.g. `Missing required flag --intent. Usage: …`, `Intent file not found: …`).
  - every invocation also prints the app banner `Welcome to Code Worker CLI`.
- **The process still exits 0 in every case** — usage errors are logged, not surfaced as a non-zero
  exit. This is deliberate (`verify-command-surface` ADR-5); the non-zero exit is a later Foundation
  item (**Verdict model + reporting**). The E2E suite therefore **asserts on stdout substrings** and
  **records the current exit-0-always behavior** as the contract-of-today, so that when the verdict
  item lands, the failing E2E assertions point exactly at what changed.

## Acceptance Criteria

- [ ] A new **`CodeWorker.Cli.EndToEnd.Tests`** project exists in `CodeWorker.sln`, and its tests are
      tagged with an xUnit **`Category=EndToEnd`** trait so the fast unit loop can exclude them
      (`dotnet test CodeWorker.sln --filter "Category!=EndToEnd"`) and the E2E loop can select them
      (`--filter "Category=EndToEnd"`).
- [ ] The suite **publishes the CLI once per test run** (`dotnet publish` of
      `CodeWorker.Cli/CodeWorker.Cli.csproj` to a temporary directory) via a shared xUnit fixture,
      exposes the resulting `FatCatCodeWorkerCli.exe` path, and deletes the temp directory on teardown.
      No test project-references the CLI (it is a black box); the CLI is located by walking up to the
      directory that contains `CodeWorker.sln`.
- [ ] A reusable process runner invokes the published exe with an argument array, captures
      **stdout**, **stderr**, and **exit code** without deadlocking, and returns them as a value.
- [ ] **Green path:** running `verify --intent <i> --production <p> --tests <t>` against three files
      that exist on disk prints `verify: parsed intent …` containing all three paths on stdout.
- [ ] **Missing-flag paths:** omitting `--intent`, `--production`, or `--tests` prints the specific
      `Missing required flag <flag>` usage message for that flag on stdout — one independently
      assertable test per flag.
- [ ] **Missing-file paths:** a flag present but pointing at a non-existent file prints the specific
      `Intent file not found:` / `Production file not found:` / `Tests file not found:` message.
- [ ] **No-arguments path:** invoking the exe with no arguments prints the `Usage:` line.
- [ ] Every E2E test asserts the process **exit code is 0** (documenting today's ADR-5 behavior), so
      the suite is the tripwire that fails the day a non-zero exit is wired.
- [ ] The E2E suite passes (`dotnet test CodeWorker.sln --filter "Category=EndToEnd"`), the fast unit
      suite still passes with the E2E tests excluded, and the whole solution builds with zero warnings.

## Out of Scope / Non-Goals

- **Testing gates that do not exist yet.** There is no compile / test / mutation / intent gate in the
  CLI today — only the argument surface. The E2E suite covers only the observable `verify` argument
  behavior. New gates get their own E2E tests as they land (each Foundation item extends this suite).
- **Asserting a non-zero exit code.** Until the **Verdict model + reporting** item wires it, the
  process exits 0 on a usage error. This suite pins the exit-0 behavior; it does not change it.
- **Changing any production code.** This is a test-only item. If a production change is needed to make
  the CLI testable, stop and raise it — do not fold it in here.
- **Installing / publishing the CLI to its real install directory.** That is the **Publish + install
  to a known directory** Foundation item. This suite publishes to a throwaway temp directory it owns.
- **Cross-platform execution.** The environment is Windows; the harness targets `FatCatCodeWorkerCli.exe`.
  A `dotnet <dll>` fallback is noted for portability but not built or tested here.
- **Performance / the sub-10s budget.** E2E tests are intentionally slow (they publish and spawn
  processes); they are excluded from the fast loop precisely so they never sit in the write-loop path.

## Feature Context

- **Not an `epics.md` feature epic.** `epics.md` tracks the `verify` feature build; end-to-end testing
  is cross-cutting test tooling, planned here as a standalone item under `tasks/todo/tooling/`. There
  is no `epics.md` checkbox and no `00-epic-plan.md` to update for this item.
- **Source of truth:** [`CodeWorker.Cli/README.md`](../../../../CodeWorker.Cli/README.md).
- **The surface under test** was built by
  [`tasks/todo/foundation/verify-command-surface/`](../../foundation/verify-command-surface/00-overview.md)
  — see its ADR-5 (exit code deferred) and ADR-2 (unknown verb routes to verify).
- **Standards:** every phase obeys `CLAUDE.md` and `.claude/rules/csharp/*.md` (xUnit + FluentAssertions,
  verb-first one-assertion tests, block bodies, `[ExcludeFromCodeCoverage]` for `System.Diagnostics.Process`
  wrappers per `testing.md`).

# Notes

- The published executable is **`FatCatCodeWorkerCli.exe`** — the CLI csproj sets
  `<AssemblyName>FatCatCodeWorkerCli</AssemblyName>`, not `FatCatCodeWorker`.
- The injected `ILogger` is a plain **synchronous** Serilog console sink
  (`new LoggerConfiguration().WriteTo.Console().CreateLogger()` in `CodeWorkerCliModule`), so stdout is
  complete by the time the process exits — no async-sink flush race. If a future change makes the sink
  async, the harness may need to wait/flush; note it then.
- Assert with `.Contain(substring)`, never exact-equality against a whole line — the Serilog default
  output template prefixes a timestamp and level (`[HH:mm:ss LVL] …`) that must not be baked into
  assertions.
- The FatCat toolkit source is at `C:\Code\FatCat.Toolkit\src` if a toolkit detail must be verified.
