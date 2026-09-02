---
name: epic-planning
description: Plan an epic from epics.md incrementally, one checklist item at a time, following the Apostil convention. Use when the user says "epic planning for <group>", "plan the <group> epic", "plan the next item", "plan <item>", or similar. Has two modes — (A) write the epic plan: a roadmap of the group's checklist items in dependency order, and (B) plan one item: expand a single checklist item into a task.md specification plus a phased implementation plan (00-overview + NN-phase files + orchestrator) under tasks/todo/. Plans one item at a time on purpose so a change of direction never strands detailed plans for items not yet reached. This skill writes ONLY planning documents — it NEVER writes production code and NEVER runs an orchestrator.
---

# Epic Planning

Turn an epic from [`epics.md`](../../../epics.md) into actionable plans, following the **Workflow** at the top of that file and the `task.md` template. This is a **specification-and-planning** skill: it writes planning documents only. It never writes production code, never commits, and never runs an orchestrator — running is a separate, explicit step the user drives.

## The guiding principle — plan one item at a time

An epic is a `###` group of checklist items in `epics.md` (e.g. **Foundation** under MVP). The whole point of this skill is to **avoid expanding every item into a detailed plan up front.** Detailed, phased plans are written **one checklist item at a time**, only when that item is next. Items not yet reached stay as one-line roadmap entries. This is deliberate: if the user changes direction after building an early item, no downstream detailed plan is stranded or has to be thrown away.

So the skill has two modes:

- **Mode A — Write the epic plan (the roadmap).** Produced once per epic. Lists the group's checklist items in dependency order, records group-level decisions, and names which item is next. **No phase files.**
- **Mode B — Plan one item.** Produced each time the user is ready for the next item. Fills out `task.md` as that item's specification and produces its Apostil-style phased plan (`00-overview.md`, `NN-*.md` phase files, `orchestrator.md`).

## Step 1 — Determine the mode and resolve the target

Read the user's phrasing:

| User says | Mode | Target |
|---|---|---|
| "epic planning for foundation" / "plan the foundation epic" | A | The `### Foundation` group in `epics.md` |
| "plan the next item" / "plan the next foundation item" | B | The first not-`[x]` item in that group without a plan yet |
| "plan <item name>" / "plan verify command surface" | B | The named checklist item |
| "re-plan <item>" / "the plan for <item> is wrong, redo it" | B | The named item (overwrite its existing plan) |

Rules:
- The "epic" is a `###` group heading in `epics.md`. If the user names a single checklist item as the epic, treat it as a Mode B target directly.
- If the group name is ambiguous or missing, list the available `###` groups from `epics.md` and ask which one.
- If a Mode A epic plan does not exist yet and the user asks for Mode B, write the epic plan first (Mode A), then continue into the requested item.
- If an ambiguity would **materially change the design**, stop and ask rather than guess (`task.md` planning rule).

## Step 2 — Read the references (both modes, always)

These are the sources of truth. Read them before planning — do not rely on memory.

- **`epics.md`** (repo root) — the epic list, the group, its checklist items, and the resolved Open Questions that constrain the design.
- **`CodeWorker.Cli/README.md`** — the source of truth for *what* the CLI is and does. Every plan is grounded here. (For work in the console app instead of the CLI, read the root `README.md`.)
- **`task.md`** (repo root) — the work-item / phase template. Its "Phase Requirements", "Definition of Done", and "Orchestrator" sections are mandatory shape for Mode B.
- **`.claude/rules/csharp/*.md`** (and `powershell/`, `typescript/` if relevant) — the coding standards every phase must satisfy. The Definition of Done references these.
- **The Apostil reference** at `C:\Code\apostil\tasks\done\` — worked examples of the overview + phase + orchestrator shape (`api_logging/`, `token_refresh/`, `dev_token/`). Match their structure and depth, not their content.
- Any files the target item depends on in the current codebase — read the actual code (it is authoritative over a stale README).

## Step 3 (Mode A) — Write the epic plan

Produce one file: `tasks/todo/<group-slug>/00-epic-plan.md` (create the folder; `<group-slug>` is the kebab-case group name, e.g. `foundation`).

The epic plan is a **roadmap**, not a set of phase plans. It contains:

- The group's checklist items **in dependency order**, each with: a one-paragraph scope, its dependencies on other items, the acceptance shape (what "done" looks like), and a `Plan:` field that is `not yet planned` initially and becomes a link to the item's folder once Mode B runs.
- **Group-level decisions (lightweight ADRs)** that span more than one item — the cross-cutting shape choices (interfaces, folder placement, naming) grounded in the README and `.claude/rules`. Per-item ADRs belong in that item's `00-overview.md`, not here.
- **Assumptions and open questions** for the group. Flag anything that would change the design.
- **The build order and the "next" pointer** — which item is planned next, and the one-at-a-time rule restated so anyone reading knows downstream items are intentionally still one-liners.

Do **not** create per-item folders or phase files in Mode A. After writing, tell the user the epic-plan path and name the next item to plan.

### Epic-plan template

```markdown
# <Group> — Epic Plan

- **Epic (group):** <### heading> in `epics.md` (<MVP|Full>)
- **Source of truth:** `CodeWorker.Cli/README.md`
- **Generated:** <timestamp>
- **Workflow:** plan one item → build it → adjust → plan the next. Items below that are not yet
  planned stay as one-liners on purpose (see "Ordering rule").

## Items (dependency order)

### 1. <Item name> — `<item-slug>`
- **Scope:** <one paragraph, grounded in the README>
- **Depends on:** <other items, or —>
- **Acceptance shape:** <what proves it done>
- **Plan:** not yet planned  <!-- becomes: tasks/todo/<group-slug>/<item-slug>/ -->

### 2. <Item name> — `<item-slug>`
...

## Group decisions (lightweight ADRs)

### ADR-G1 — <decision that spans items>
**Decision:** ...
**Context:** ...
**Alternatives rejected:** ...

## Ordering rule

Detailed phased plans are written one item at a time (Mode B), only when the item is next.
Reverting or re-planning a built item never strands a downstream plan, because downstream items
are not planned until reached.

## Next item

**<item-slug>** — say "plan <item name>" (or "plan the next <group> item") to expand it.

## Assumptions & open questions

- <assumption / question; flag design-changing ones>
```

## Step 4 (Mode B) — Plan one item

Expand exactly **one** checklist item into a task.md specification plus a phased plan. Everything lands under `tasks/todo/<group-slug>/<item-slug>/`.

### 4a — Specification (fill out task.md for this item)

Write `tasks/todo/<group-slug>/<item-slug>/task.md` using the `task.md` template's shape: the Work Item slug, the Specification (in the README's terms), testable Acceptance Criteria, explicit Out-of-Scope, and Feature Context links. This is the item's source of truth; the phases prove its acceptance criteria.

### 4b — Overview (00-overview.md)

Write `00-overview.md` in the item folder, matching the Apostil overview shape:

- **Work Item** — restate the scope and the *current state of the code* that shapes the design (verified against the actual files, not assumed).
- **Acceptance Criteria → Phase Map** — a table mapping each acceptance criterion to the phase(s) that prove it.
- **Phases & Dependency Graph** — a table: phase #, file, risk (low/medium/high), `Depends on`, `Depended on by`, plus the revert cascade in reverse dependency order.
- **Orchestrator** — one line on how to run it (see 4d).
- **Decisions (lightweight ADRs)** — per-item decisions: decision, context, alternatives rejected.
- **Assumptions** and **Open Questions** — flag design-changing ambiguities; do not guess.

### 4c — Phase files (NN-<slug>.md)

Split the item into a **handful of atomic phases** (≈3–6; `epics.md` says an epic that can't be is too big — flag it). Number them `01-…`, `02-…`. Each phase file must be, per `task.md` Phase Requirements:

- **Context-isolated** — the complete handoff; a fresh session with only this file can execute it.
- **Atomic** — exactly one commit; the commit message references the phase file.
- **Independently revertible** — declares `Depends on:` / `Depended on by:`.
- **Reversible** — a `## Rollback Procedure` (the git revert target + any manual steps).
- **Verifiable** — a `## Definition of Done` checklist (see below).
- **Contract-defining** — a `## Hand-off` section: the interfaces, types, and routes it exposes to later phases.
- **Risk-rated** — low/medium/high with the reason; anything touching auth, anonymous endpoints, data migration, or public API contracts is automatically high.

Every phase's `## Definition of Done` uses the `task.md` "Definition of Done (every phase)" checklist verbatim in spirit — TDD (red before green), zero warnings, namespaces match folders, all `.claude/rules/csharp` rules, no banned patterns, `dotnet test` green, `dotnet format` style + analyzers, `dotnet build` for CSharpier, then the **review loop** (`unit-test-review` must end `Unit test review: PASS` → `code-review` → `code-security-review`, restarting from the top after any fix), one commit on the task branch, no push.

Follow the phase-file template below (adapted from Apostil `03-logging-level-endpoint.md`).

### 4d — Orchestrator (orchestrator.md)

Write `orchestrator.md` in the item folder: the runbook that executes the phases in dependency order, **one fresh general-purpose subagent per phase** (context isolation is not optional), verifying exactly one new commit and a clean working tree after each phase, halting on failure, never squashing/amending/rebasing/pushing. Trigger: "run <item-slug>". Model it on Apostil's `orchestrator.md` (ground rules, per-phase procedure, halt-on-failure, completion report).

### 4e — Update the epic plan

In `00-epic-plan.md`, change that item's `Plan:` from `not yet planned` to the item folder path, and update the **Next item** pointer to the following item. Do **not** flip the `epics.md` checkbox — that happens only when the item's work is actually built and merged (the human's call, per the `epics.md` workflow).

### Phase-file template

```markdown
# Phase <N> — <Title>

- **Work item:** <item-slug> (see `tasks/todo/<group-slug>/<item-slug>/00-overview.md`)
- **Depends on:** <phase(s), or —>
- **Depended on by:** <phase(s), or —>
- **Risk:** <low|medium|high> — <why; auto-high triggers if applicable>

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and all `.claude/rules/csharp/*.md` first — mandatory.
<Current state of the code this phase builds on, verified against the files. Interfaces/types
already present. The exact pattern in the codebase to copy.>

## Design (build exactly this shape)

<The types, interfaces, folders, and namespaces to create — with short code sketches in the house
style. Name every file and its namespace. Note deviations to verify against the real toolkit/code.>

## Steps (TDD — tests first, red before green)

1. <failing tests first, named verb-first, one assertion each>
2. <implement to green>
3. <manual smoke check if useful>

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln` — all tests pass
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; correct type-role suffixes; no banned patterns
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current task branch, message referencing this file; no push

Suggested commit message:

```
<item-slug> phase <N>: <summary> (tasks/todo/<group-slug>/<item-slug>/<NN-slug>.md)
```

## Rollback Procedure

- <revert dependent phases first if any, then `git revert <this phase's commit>`; manual steps>

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (every departure from
this plan and why — an empty log is a claim, not a default); open questions/risks for the reviewer.

## Hand-off

- **Interfaces/types/routes this phase exposes to later phases:** <the contract>
- **Behavior notes for later phases:** <anything they must know>
```

## References this skill reads

- `epics.md`, `task.md`, `CodeWorker.Cli/README.md`, `README.md` (repo root)
- `.claude/rules/csharp/*.md` (+ `powershell/`, `typescript/` when the item touches them)
- `C:\Code\apostil\tasks\done\{api_logging,token_refresh,dev_token}\` — reference structure

## Hard rules for this skill

- **Planning documents only. Never write production code, never edit source files, never commit, never run an orchestrator.** Those are separate, explicit steps the user drives.
- **One item at a time.** In Mode B, expand exactly one checklist item. Never batch-expand the whole group into phase plans — that defeats the "don't stall" purpose.
- **Never flip an `epics.md` checkbox to `[x]`.** That marks *built and merged* work; only the human does it after the item ships.
- **Ground every plan in `CodeWorker.Cli/README.md` and `.claude/rules`.** Where the README and the code disagree, the code is authoritative — plan against the code and flag the stale README.
- **Match the surrounding abstraction level.** Do not invent patterns or abstractions that are not already in the codebase (per `naming-and-structure.md`).
- **Stop and ask on design-changing ambiguity** rather than guessing (per `task.md` planning rules).
- Timestamps: `pwsh -Command '. $PROFILE; (Get-Date).ToString("yyyy-MM-dd-HHmmss")'`.
