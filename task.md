# Work Item

<!-- Short kebab-case identifier; becomes the plan folder name -->

# Specification

<!-- What the product manager / stakeholder asked for, in their words.
     Screens, behaviors, edge cases. This is the source of truth. -->

## Acceptance Criteria

<!-- Testable statements of done. Given/When/Then where it helps.
     The E2E walk at the end of the plan must prove these. -->

- [ ]

## Out of Scope / Non-Goals

<!-- Explicitly what NOT to build. Prevents gold-plating and scope drift. -->


## Feature Context

<!-- Links to wireframes, workflow docs, prior art -->

# Planning Instructions

This is a **specification and planning task. No production code is written in this session.**

- [ ] Read all references before planning
- [ ] Write a phased implementation plan to `tasks\todo\{WORK_ITEM}` (one file per phase plus `00-overview.md`)
- [ ] Record cross-cutting decisions in `00-overview.md` (treat these as lightweight ADRs — architecture decision records: decision, context, alternatives rejected)
- [ ] List **assumptions and open questions** in the overview. If an ambiguity materially changes the design, stop and ask rather than guess
- [ ] Keep commits small.

## Phase Requirements

Each phase must be:

- [ ] **Context-isolated** — executable in a fresh session with no state from prior sessions; the phase file is the complete handoff document
- [ ] **Atomic** — exactly one commit per phase; the commit message references the phase file
- [ ] **Independently revertible** — the phase declares its position in the **dependency graph** (`Depends on:` / `Depended on by:`) so a revert can cascade correctly (reverting phase 3 tells me to also revert 4 and 5 if they depend on it)
- [ ] **Reversible** — each phase includes a `## Rollback Procedure` section (the "down migration"): exact `git revert` target plus any manual steps (data, config, feature flags)
- [ ] **Verifiable** — each phase includes verification steps and a `## Definition of Done` checklist
- [ ] **Contract-defining** — each phase ends with a `## Hand-off` section: the interfaces, types, and routes it exposes to later phases
- [ ] **Risk-rated** — each phase declares a risk level (low / medium / high) and why. Anything touching auth, anonymous endpoints, data migration, or public API contracts is automatically high and flagged for extra human review

## Orchestrator

- [ ] Create a phase orchestrator (runbook or script) that executes phases in dependency order
- [ ] The orchestrator MUST spawn a **fresh session per phase** (context isolation is not optional — never run multiple phases in one session/context window)
- [ ] Commit boundary between every phase; the orchestrator never squashes or amends
- [ ] **Halt-on-failure policy:** if a phase's Definition of Done cannot be met after 2 self-correction attempts, STOP the pipeline, leave the working tree clean (stash or discard), write a failure report, and do not start dependent phases
- [ ] The orchestrator never force-pushes, never rebases published history, and never pushes to remote

# Definition of Done (every phase)

## Quality Gates

- [ ] Tests written before implementation (TDD — red before green)
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] Must follow all rules in `.claude\rules\csharp` — no exceptions
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] `dotnet build` to apply CSharpier changes
- [ ] Run `unit-test-review` on uncommitted code; it must end with `Unit test review: PASS` — resolve every gap, defect, and conformance finding (write the missing tests) before moving to `code-review`
- [ ] Run `code-review` on uncommitted code; resolve any findings before commit
- [ ] Run `code-security-review` after `code-review` passes; any vulnerabilities found MUST be resolved before commit
- [ ] **Review loop:** code changes → `unit-test-review` until it passes → `code-review` until it passes → `code-security-review`. Any fix made at a later stage sends you back to the start of the loop (`unit-test-review`), because fixes are code changes that need tests too. Repeat until all three reviews pass with no findings — only then commit

## Commit Policy

- [ ] Work happens on the task's feature branch (`task/{WORK_ITEM}`), never on main  (only create the branch if not on main if you are on a different branch then main use the current branch)
- [ ] Create the phase commit locally; **do not push to remote** — human review gates the push

## Phase Report

Before finishing, produce a structured report:

- [ ] Files added / changed / deleted
- [ ] Test counts (new, total, passing)
- [ ] **Deviation log** — every place the implementation departed from the plan file, and why (an empty deviation log is a claim, not a default)
- [ ] Open questions or discovered risks for the human reviewer

# References

- Read the `README.md` for an overview of the application

# Notes

- Use components as needed to simplify changes and for look and feel.
- I control FatCat.Toolkit the source is found here `C:\Code\FatCat.Toolkit\src` if making a change to the toolkit would then suggest those changes.  A task would be written in `C:\Code\FatCat.Toolkit\src\task\todo` following the same rules found in this document.

