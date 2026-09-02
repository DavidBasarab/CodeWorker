# cli-end-to-end-testing — Overview

- **Work item:** `cli-end-to-end-testing` (standalone tooling item — **not** an `epics.md` epic)
- **Source of truth:** `CodeWorker.Cli/README.md`
- **Generated:** 2026-09-02-144807

## Work Item

Stand up the CLI's **first black-box end-to-end suite**: a `CodeWorker.Cli.EndToEnd.Tests` project
that **publishes the real `FatCatCodeWorkerCli.exe`** to a temp directory once per run and **invokes
it as a subprocess**, asserting on the **stdout** and **exit code** a real caller would see for the
`verify` command that exists today. It complements — does not replace — the faked unit tests in
`CodeWorker.Cli.Tests`.

### Current state that shapes the design (verified against the code)

- **`Program.Main`** (`CodeWorker.Cli/Program.cs`) initializes `SystemScope`, resolves
  `CodeWorkerCliApplication`, `await`s `Run(args)`, catches any exception to `ConsoleLog.WriteException`,
  and in `finally` calls `Log.CloseAndFlush()` + `Console.Out.Flush()`. It **never sets an exit code** —
  so the process exits 0 on both the happy path and every usage error today.
- **`CodeWorkerCliApplication.Run`** logs `"Welcome to Code Worker CLI"` (Information) then
  `await processArguments.Process(args)`. The banner appears on stdout for **every** invocation.
- **`ProcessArguments.Process`** resolves via `IResolveCommand` and executes the command.
  **`CommandResolver`** returns the verify command for `"verify"` **and** for the default arm and for
  empty args (`verify-command-surface` ADR-2) — so any invocation reaches `VerifyCommand`.
- **`VerifyCommand.Execute`** calls `ParseVerifyArguments.Parse(args)`. On `IsValid` it logs
  `Information("verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}", …)`;
  otherwise it logs `Error("verify: {Reason}", result.Message)` and returns. **No exit code, no throw.**
- **`ParseVerifyArguments`** (the message strings the E2E suite asserts on):
  - `NoArguments` → `"Usage: verify --intent <intent.json> --production <Foo.cs> --tests <FooTests.cs>"`
    (returned when **none** of the three flags are present).
  - missing flag → `"Missing required flag --intent. {UsageLine}"` (and `--production` / `--tests`).
  - missing file → `"Intent file not found: {path}. {UsageLine}"` (and `Production file not found:` /
    `Tests file not found:`), checked via `IFileSystemTools.FileExists` — i.e. the file must **really
    exist on disk** for the green path, which is exactly what the E2E harness arranges with temp files.
  - The parser scans the whole args array for flags, so the leading `verify` verb is harmless; both
    `verify --intent …` and bare `--intent …` parse identically.
- **`CodeWorkerCliModule`** registers a plain synchronous Serilog console logger
  (`WriteTo.Console()`); everything else auto-registers via `SystemScope` scanning.
- **The CLI project** (`CodeWorker.Cli.csproj`) is `OutputType=Exe`, `net10.0`,
  `AssemblyName=FatCatCodeWorkerCli` → published host is **`FatCatCodeWorkerCli.exe`** on Windows.
- **The test stack** (`CodeWorker.Cli.Tests`): xUnit 2.9.3, FakeItEasy, FluentAssertions, FatCat.Fakes,
  `Microsoft.NET.Test.Sdk` 18.3.0, `xunit.runner.visualstudio` 3.1.5, CSharpier.MsBuild. The new E2E
  project mirrors these (minus FakeItEasy — nothing is faked in a black-box test).
- **The solution** is legacy `.sln` format with four projects; the new project is added the same way
  (project GUID + `Debug|Any CPU` / `Release|Any CPU` config rows).

## Acceptance Criteria → Phase Map

| Acceptance criterion (task.md) | Proven by |
|---|---|
| New `CodeWorker.Cli.EndToEnd.Tests` project in the `.sln`, `Category=EndToEnd` trait, filterable | Phase 1 |
| Publishes the CLI once per run to a temp dir; locates it via the `.sln` anchor; cleans up | Phase 1 (`PublishedCli` fixture) |
| Reusable process runner captures stdout/stderr/exit without deadlock | Phase 1 (`CliProcessRunner` + `CliResult`) |
| Harness actually works end-to-end (publish → invoke → capture) | Phase 1 (banner smoke test) |
| Green path prints `verify: parsed intent …` with all three paths | Phase 2 |
| Missing `--intent` / `--production` / `--tests` prints the specific flag message | Phase 3 |
| Missing intent / production / tests file prints the specific not-found message | Phase 3 |
| No-arguments prints the `Usage:` line | Phase 3 |
| Every E2E test asserts exit code 0 (documents ADR-5) | Phases 1, 2, 3 |
| E2E suite green; fast unit suite green with E2E excluded; zero warnings | Every phase's Definition of Done |

## Phases & Dependency Graph

| Phase | File | Risk | Depends on | Depended on by |
|---|---|---|---|---|
| 1 — E2E project + publish/run harness (project in `.sln`, `EndToEnd` trait, `PublishedCli` fixture, `CliProcessRunner`/`CliResult`, banner smoke test) | `01-harness-spine.md` | **Medium** — new project + solution wiring + a `dotnet publish` fixture and `System.Diagnostics.Process` invocation (the parts most likely to be flaky: exe path, stream capture, temp cleanup). Test-only; no production code, no auth/network/data surface. | — | 2, 3 |
| 2 — Green-path E2E (valid `verify` invocation over three real temp files) | `02-valid-invocation.md` | **Low** — one test built entirely on the Phase 1 harness. | 1 | — |
| 3 — Usage-error E2E (missing flag ×3, missing file ×3, no-arguments) | `03-usage-errors.md` | **Low** — data-driven assertions on stdout via the Phase 1 harness. | 1 | — |

**Revert cascade (reverse dependency order):** revert 3 and 2 (independent of each other, either order)
before 1. Reverting 1 while 2 or 3 stand would break the build — revert the leaf phases first. Phase 1
alone is a coherent, compilable increment (harness + one passing smoke test).

## Orchestrator

`orchestrator.md` (this folder) is the runbook. Usage: open a Claude session in this repo and say
**"run cli-end-to-end-testing"** — the session drives the phases in dependency order (1 → 2 → 3), each
in a **fresh, isolated context** (one general-purpose subagent per phase; the phase file is the entire
handoff), verifying exactly one new commit and a clean working tree after each phase, halting on
failure. It never squashes, amends, rebases, or pushes.

## Decisions (lightweight ADRs)

### ADR-1 — A separate `CodeWorker.Cli.EndToEnd.Tests` project, in the solution, trait-gated
**Decision:** Create a dedicated `CodeWorker.Cli.EndToEnd.Tests` project (mirroring the existing test
projects' packages, `RootNamespace = Testing.FatCat.CodeWorker.Cli.EndToEnd`) and add it to
`CodeWorker.sln`. Every test class carries `[Trait("Category", "EndToEnd")]`. The fast loop runs
`dotnet test CodeWorker.sln --filter "Category!=EndToEnd"`; the E2E loop runs `--filter "Category=EndToEnd"`.
**Context:** E2E tests publish and spawn processes — seconds each. Mixed into the one assembly the
unit tests live in, they would slow every phase's DoD loop and the write-loop the CLI is meant to sit
in. A separate, filterable project keeps the fast feedback fast (user decision, 2026-09-02).
**Alternatives rejected:** a folder inside `CodeWorker.Cli.Tests` (one assembly mixes millisecond
faked tests with multi-second process tests; no clean fast/slow split); a project outside the `.sln`
(loses one-command discoverability and IDE integration).

### ADR-2 — Black box: publish the real exe, no `ProjectReference` to the CLI
**Decision:** The E2E project does **not** reference `CodeWorker.Cli`. A shared fixture runs
`dotnet publish CodeWorker.Cli/CodeWorker.Cli.csproj -c Release -o <temp>` once per run and invokes
`<temp>/FatCatCodeWorkerCli.exe`. The CLI project is located by walking up from `AppContext.BaseDirectory`
to the directory containing `CodeWorker.sln`.
**Context:** The whole point is to exercise the CLI the way Claude does — as an installed, published
executable invoked from a known directory (README: "Configured From the Outside"). A `ProjectReference`
+ in-process `Program.Main` call would test the assembly, not the shipped tool, and would share the
test host's DI/logging rather than the real boot path. Publishing once and sharing it across tests via
an xUnit collection fixture keeps the cost to a single publish (user decision, 2026-09-02).
**Alternatives rejected:** reusing `CodeWorker.Cli/bin/**` build output (couples to build layout and
assumes a prior build); `dotnet run --project …` per test (restore/build overhead per invocation, least
production-like); in-process `Program.Main` (not black box).

### ADR-3 — Assert stdout substrings; pin exit code 0 (today's contract)
**Decision:** Tests assert `result.StandardOutput.Should().Contain("<message fragment>")` against the
message strings in the code, and assert `result.ExitCode.Should().Be(0)` in every case — including
usage errors.
**Context:** Exit codes are deferred (`verify-command-surface` ADR-5): a bad invocation logs an error
but exits 0. Stdout is the only stable contract the surface exposes today (user decision, 2026-09-02).
Pinning exit-0 turns this suite into the tripwire that fails — visibly and exactly — the day the
**Verdict model + reporting** item wires a non-zero exit, at which point these assertions are updated
as part of that item's work (flagged in Open Questions).
**Alternatives rejected:** asserting a non-zero exit now (the code does not do that yet — the test would
be red against correct current behavior); exact full-line equality (brittle against Serilog's
timestamp/level prefix); pulling exit-code wiring forward into this test-only item (out of scope; it
belongs to the verdict item).

### ADR-4 — Harness classes are `[ExcludeFromCodeCoverage]` process/publish wrappers; the E2E tests are the coverage
**Decision:** `PublishedCli` (wraps `dotnet publish`) and `CliProcessRunner` (wraps
`System.Diagnostics.Process`) are marked `[ExcludeFromCodeCoverage]` with a specific justification and
have **no separate unit tests**. The E2E test methods that drive them are the behavioral coverage.
**Context:** `testing.md` — "Low-Level API Implementations — No Unit Tests Required": classes that only
talk to a low-level external system (here, the `dotnet` CLI and the OS process API) carry no branching
business logic worth faking; they exist to be exercised by the tests that use them. Keep any real logic
(locating the `.sln` root, building the message expectations) trivial and inline, or it must be tested.
**Alternatives rejected:** unit-testing the process wrapper by faking `Process` (no seam worth the
abstraction for a one-file wrapper; over-engineering per `naming-and-structure.md`).

## Assumptions

- **Windows environment** — the published host is `FatCatCodeWorkerCli.exe`. The runner may compute the
  host name from the OS (`.exe` on Windows) for robustness; a `dotnet FatCatCodeWorkerCli.dll` fallback
  is noted but not built.
- **`dotnet` is on PATH** in the test host (true here — the SDK builds the solution). The publish
  fixture shells out to `dotnet publish`; if it fails, it throws with the captured output so the failure
  is diagnosable rather than a mysterious "file not found" on the exe.
- **The Serilog console sink is synchronous**, so stdout is complete at process exit (verified in
  `CodeWorkerCliModule`). No flush/delay handling is needed unless the sink becomes async later.
- **Nullable is disabled** and **`ImplicitUsings` is enabled** in the CLI projects; the E2E project
  follows suit. A `GlobalUsings.cs` covers the test stack (xUnit, FluentAssertions), mirroring
  `CodeWorker.Cli.Tests`.
- **Work happens on the current branch `CliTester`** (not `main`), per the commit policy.
- **CSharpier uses tabs** (`.csharpierrc`) — write readable code and let it format on build.

## Open Questions

None blocking. Flags for the human reviewer:

- **Exit-0 pinned (ADR-3):** every E2E test asserts exit code 0, matching today's behavior. When the
  **Verdict model + reporting** Foundation item wires a non-zero exit on a failed verdict, these
  assertions must be updated **as part of that item** — this suite is intentionally the thing that goes
  red to force that update. Not a defect; a design choice.
- **Publish configuration (ADR-2):** the fixture publishes `-c Release`. If you prefer the E2E suite to
  exercise the same `Debug` artifacts the rest of the loop builds, that is a one-line change in the
  fixture — called out so the choice is conscious, not accidental.
- **Publish cost:** one `dotnet publish` per test run (shared via a collection fixture) adds a fixed
  few-seconds startup to the E2E loop only. The fast unit loop is unaffected because it filters E2E out.
- **Not in `epics.md`:** this is standalone test tooling, so no `epics.md` checkbox is flipped. If you
  want E2E testing tracked as a first-class line item there, add it under a tooling/quality group — say
  the word and it can be planned into the epic list.
