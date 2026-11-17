# Work Item

`verdict-model-reporting`

# Specification

Close the `verify` spine: turn the gate pipeline's result (item 3) into one **verdict**, and deliver
it three ways (README "Usage"):

1. a **machine-readable JSON verdict** for the Claude Code `Stop` hook to feed back to the AI —
   written to the file named by a new optional **`--verdict <path>`** flag (user decision
   2026-11-17: a file the hook passes a temp path for, so stdout's banners and log lines never have
   to be parsed);
2. a **human-readable report** on the console, for the person reviewing the work the next morning;
3. the **process exit code** — **0 on green, non-zero on any block** (README). The `Stop` hook and CI
   key on it.

An **invalid intent** is the AI's own mistake, so it is fed back the same way: it produces a verdict
(status `invalidIntent`, the intent error as the message) and a non-zero exit. A **usage error** (bad
flags, missing files) is the caller's mistake: no verdict file (the flags may not even parse), the
existing usage message, and a non-zero exit. An **unexpected crash** must never read as green: it
exits non-zero too.

Exit codes (distinct, so the hook can tell a red gate from a broken invocation):

| Code | Meaning |
|---|---|
| 0 | Green — every gate that ran passed or warned |
| 1 | Blocked — a gate failed |
| 2 | Invalid input — usage error or invalid intent |
| 3 | Unexpected error — an exception reached `Program.Main` |

## Acceptance Criteria

- [ ] `verify … --verdict <path>` is accepted; the flag is optional, its value is the verdict file
      path, and the target file need not exist. The usage line mentions it.
- [ ] A green pipeline result builds a `green` verdict; any `Failed` gate builds `blocked`; an invalid
      intent builds `invalidIntent` carrying the intent error message.
- [ ] A blocked verdict's message names the first failed gate and its first finding (file, line,
      reason) — what the AI fixes next.
- [ ] Each gate appears in the verdict with name, stage, status, duration in milliseconds, and
      findings, in run order (skipped gates included).
- [ ] The JSON verdict uses camelCase property names and camelCase enum strings, omits null values,
      and is written to the `--verdict` path when (and only when) the flag is given.
- [ ] The human-readable report states the status and message, then one line per gate (status, name,
      duration) and one indented line per finding.
- [ ] Exit code: 0 green, 1 blocked, 2 usage error or invalid intent, 3 unexpected exception.
- [ ] E2E: a valid invocation with `--verdict` writes `{"status":"green",…}` and exits 0; an invalid
      intent writes `{"status":"invalidIntent",…}` and exits 2; a usage error exits 2 and writes no
      verdict file; the report's status line appears on stdout.
- [ ] Unit-tested with fakes (`IFileSystemTools`, `ISetExitCode`, the verdict collaborators); zero
      warnings; `dotnet test` green including E2E.

## Out of Scope / Non-Goals

- The `Stop` hook itself, max-attempts, and **BLOCKED – needs human** stamping — Integration group.
- Block-vs-warn configuration — Integration group (tool-level config).
- Per-gate timing *analysis* — Full; this item only carries the measured duration through.
- Writing the human report to a file — stdout only for now.
- Any concrete gate — later groups.
- Language engine resolution — item 5 (`language-engine-seam`), built in parallel.

## Feature Context

- Epic plan: [`tasks/todo/foundation/00-epic-plan.md`](../00-epic-plan.md) — item 4, ADR-G2..G5.
- Built on: [`gate-pipeline-framework`](../gate-pipeline-framework/00-overview.md) (item 3) —
  `GatePipelineResult`, `GateResult`, `GateFinding`, `GateStage`, `GateStatus`.
- Source of truth: [`CodeWorker.Cli/README.md`](../../../../CodeWorker.Cli/README.md) — "Usage",
  "How Claude actually hits it — a `Stop` hook".
