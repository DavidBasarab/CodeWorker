# Work Item

`gate-pipeline-framework`

# Specification

Build the **spine every verification gate plugs into**: the `IVerificationGate` abstraction and a
**fail-fast pipeline runner** that runs the gates in a fixed order, stops at the first blocking
failure, and returns one structured result per gate. This item ships the framework with **zero
concrete gates** — the Compile, Test, and Mutation gates (Deterministic gates group), the Standards
gates, and the Intent judge (Full) each plug in later without changing the runner.

Grounded in `CodeWorker.Cli/README.md` ("How It Works"):

- **A fail-fast pipeline of gates.** `verify` runs an ordered set of gates; if any gate fails, the AI
  gets "the exact gate, location, and reason". A failed gate stops the pipeline — later, more
  expensive gates never run ("a broken compile never pays for a mutation run").
- **Cheapest and most objective first.** The order is fixed: Compile → Test → Mutation → Standards →
  Intent (the one LLM step runs last, only on otherwise-green code).
- **Block vs. warn.** The README's tool-level config decides "which gates block vs. warn". This item
  supports both outcomes in the runner (a warning never stops the pipeline); the config that chooses
  between them is the Integration group's **Tool-level configuration** item.
- **Per-gate result** carries pass/fail, locations, reasons, and timing (`epics.md` item text). The
  timing is what the Full-milestone **Per-gate timing in the verdict** item later surfaces.
- **Predictable outcomes are values** (ADR-G2): a failing gate is data in the result, never an
  exception. A gate that *throws* is a genuine fault and bubbles to `Program.Main`.

This item also **runs the pipeline from `verify`**: after a valid intent, `verify` builds the
verification context from the parsed arguments + intent, runs the pipeline, and logs a one-line
summary. Turning the pipeline result into the JSON verdict, the human report, and the exit code is
item 4 (`verdict-model-reporting`) — `verify` still exits 0 here.

## Acceptance Criteria

- [ ] `IVerificationGate` exposes a `Name`, a `GateStage`, and `Run(VerificationContext) → Task<GateResult>`.
- [ ] The runner runs gates in `GateStage` order (Compile → Test → Mutation → Standards → Intent)
      regardless of the order they are injected; gates sharing a stage keep their injected order.
- [ ] Every gate that runs receives the same `VerificationContext`.
- [ ] When a gate returns `Failed`, no later gate runs; each later gate still appears in the result
      as `Skipped`, and the pipeline result is **blocked**.
- [ ] A gate returning `Warning` does not stop the pipeline and does not block it.
- [ ] When every gate passes, every gate's result is returned and the pipeline is not blocked.
- [ ] With **zero gates** (the shipped state), the pipeline returns no gate results and is not blocked.
- [ ] Each result carries the gate's name, stage, status, findings (file, optional line, reason), and
      **duration** measured through an injected `IClock`; a skipped gate has zero duration.
- [ ] `verify` runs the pipeline only after valid arguments **and** a valid intent, passing a
      context that carries the three paths and the intent contract, and logs
      `verify: gate pipeline finished with {GateCount} gates, blocked: {IsBlocked}`.
- [ ] E2E: a valid invocation prints `verify: gate pipeline finished with 0 gates`; an invalid
      intent never runs the pipeline. Exit code stays 0 (item 4 owns exit codes).
- [ ] Unit-tested with faked `IVerificationGate`s and a faked `IClock`; zero warnings; `dotnet test`
      green including the E2E suite.

## Out of Scope / Non-Goals

- **Any concrete gate** (compile, test, mutation, standards, intent) — later groups.
- **The verdict**: JSON verdict, human-readable report, non-zero exit code — item 4.
- **Language engine resolution** — item 5.
- **Block-vs-warn configuration** — the Integration group's tool-level config. Gates decide
  `Failed` vs `Warning` themselves until then.
- **Caching / skipping unchanged gates** — Full, Speed & incremental.
- Catching exceptions thrown by a gate — a throwing gate is a real fault (ADR-G2).

## Feature Context

- Epic plan: [`tasks/todo/foundation/00-epic-plan.md`](../00-epic-plan.md) — item 3, ADR-G2..G5.
- Previous items (built): [`verify-command-surface`](../../../done/foundation/verify-command-surface/00-overview.md),
  [`intent-contract-model`](../../../done/foundation/intent-contract-model/00-overview.md).
- Source of truth: [`CodeWorker.Cli/README.md`](../../../../CodeWorker.Cli/README.md) — "How It Works".
- DI scanning: `C:\Code\FatCat.Toolkit\src\ToolKit\Injection\SystemScope.cs` (`BuildContainer`).
- Clock pattern to mirror: `CodeWorker/Claude/IClock.cs` (main project).
