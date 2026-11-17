# language-engine-seam Orchestrator Runbook

**Trigger:** the user says "run language-engine-seam" (optionally "… starting at wave N"), or asks to
run the Foundation item wave 4 (this item together with `verdict-model-reporting` — see the epic
plan's "Next item"). If you are the Claude session told this, follow this runbook exactly — it is
the complete instruction set. This runbook **drives** the phases; it never writes their production
code itself.

## Ground rules (non-negotiable)

- Execute in **waves** (see `00-overview.md`, Execution waves). Wave 2 starts only after Phase 1 is
  committed on the task branch and the integration gate is green.
- **Fresh context per phase.** One general-purpose subagent (Agent tool) per phase; never run a
  phase's implementation in this orchestrating session; never let one subagent touch two phases.
- **Every phase runs in an isolated worktree** (`isolation: "worktree"`), even though each wave has
  one phase. That keeps this item off whatever branch the session's main checkout is on, so it can
  run beside `verdict-model-reporting`.
- **The item checkout.** `<item checkout>` is the directory where the `language-engine-seam` branch
  is checked out:
  - run alone: the session's current directory, if it is on `language-engine-seam`;
  - run beside item 4: a sibling worktree — create it once with
    `git worktree add ../CodeWorker-language-engine-seam language-engine-seam` (absolute path
    `C:\Code\CodeWorker-language-engine-seam`). Remove it after the completion report with
    `git worktree remove ../CodeWorker-language-engine-seam`.
  All integration commands below run there (`git -C <item checkout> …`, `dotnet … ` with absolute
  paths). Never switch the main checkout's branch.
- **Worktree base.** `isolation: "worktree"` branches from `main`. Every prompt names the wave base;
  right after launching, check the worktree's `HEAD` and, if it is clean and off-base, run
  `git -C <worktree> reset --hard <wave base>` and tell the subagent to re-read what it read.
- One commit per phase; the commit message references the phase file. Integration is
  `git cherry-pick` of that commit — not a squash, amend, or rebase.
- Never push to any remote; never amend, squash, rebase, or force-push — human review gates the push.
- If `<item checkout>` is on `main`, stop and ask the user first.
- **Concurrency cap (max 3 subagents).** When run beside item 4, launch Phase 1 together with item
  4's wave 2 and Phase 2 together with item 4's wave 3 (the epic plan's combined schedule) — never
  beside item 4's three-phase wave 1.
- **Preconditions:** restored solution; `dotnet build` / `dotnet test` runnable; the `dotnet` SDK on
  PATH (the E2E suite runs `dotnet publish`).

## Waves

| Wave | Phase | Phase file | Depends on | Mode |
|---|---|---|---|---|
| 1 | 1 | `tasks/todo/foundation/language-engine-seam/01-engine-contract-and-csharp-engine.md` | — | single subagent, worktree |
| 2 | 2 | `tasks/todo/foundation/language-engine-seam/02-engine-resolver.md` | 1 | single subagent, worktree |

## Watchdog (whole run)

Keep `.claude/skills/epic-planning/watchdog.sh` armed through the Monitor tool for the whole run —
the same instance item 4 uses when both run together (one active-list file for every subagent):

```
bash .claude/skills/epic-planning/watchdog.sh <subagents dir> <active-list file> <repo> 900
```

Add `agent-<agentId>.jsonl` to the active list at launch; remove it when the subagent reports;
re-arm on every expiry; stop after the last completion report. `STALL` / `MISSING`: rule out a long
build or E2E publish, message the subagent if stuck, and if it is dead apply **Halt on failure** (or
relaunch the phase from a clean tree).

## Subagent prompt

> Execute the implementation phase described in `<phase path>`. Read that file completely first —
> it is the entire handoff document; you have no other context. Your worktree was created from
> `main`; before reading any code run `git merge --ff-only <wave base>` and confirm
> `git rev-parse HEAD` equals `<wave base>`. Follow every rule in CLAUDE.md and .claude/rules. The
> phase file's Definition of Done, including the unit-test-review → code-review →
> code-security-review loop, is mandatory. Create or modify only the files the phase file's
> `## Touches` names — phases of another item may be running in parallel. You may self-correct at
> most 2 times; if the Definition of Done still cannot be met after that, leave the working tree
> completely clean (discard or stash your changes), do NOT commit, and report PHASE FAILED with an
> explanation. On success create exactly one commit, on the branch your working directory is already
> on, whose message references `<phase path>`. Never switch branches, never amend or squash existing
> commits, and never push to any remote. Report the commit hash, branch name, the Phase Report, and
> the working directory path when you finish.

## Per-wave procedure (waves 1 and 2)

1. Record the wave base: `git -C <item checkout> rev-parse HEAD`.
2. Launch one general-purpose subagent with `isolation: "worktree"` and the prompt for the wave's
   phase (substituting the wave base); add it to the watchdog's active list. Check its worktree's
   `HEAD` against the wave base.
3. When it reports, verify the worktree:
   - `git -C <worktree> rev-list --count <wave base>..HEAD` is `1`
   - `git -C <worktree> status --porcelain` is empty
   - the commit message references the phase file; no PHASE FAILED
   - `git -C <worktree> show --stat HEAD` touches only the phase's `## Touches`
4. Integrate: `git -C <item checkout> cherry-pick <commit>`.
5. **Integration gate** in `<item checkout>`: `dotnet build <item checkout>/CodeWorker.slnx` (zero
   warnings) and `dotnet test <item checkout>/CodeWorker.slnx` (all green, including E2E);
   `git -C <item checkout> status --porcelain` empty afterwards — if CSharpier changes anything,
   halt; do not commit a fix-up.
6. Clean up: `git worktree remove <worktree path>` and `git branch -D <worktree branch>`.
7. Pass → one-line status to the user. Fail → **Halt on failure**.

## Halt on failure

1. If `<item checkout>` is dirty, run
   `git -C <item checkout> stash push --include-untracked -m "language-engine-seam wave <n> failure"`.
   A cherry-pick conflict: `git -C <item checkout> cherry-pick --abort` first.
2. Leave the failed phase's worktree in place for inspection. A Phase 1 failure means wave 2 never
   starts.
3. Write `tasks/todo/foundation/language-engine-seam/failure-report.md` (in `<item checkout>`): the
   wave and phase, what the subagent reported, which check failed, `git status` before any stash, the
   stash reference, and the worktree path left behind.
4. Report to the user. A failure here does **not** halt `verdict-model-reporting` — the items are
   independent. Stop the watchdog only when no subagent of either item is still running. Resume with
   "run language-engine-seam starting at wave N".

Special case — more than one new commit for a phase: do not revert anything; halt, report, and let
the human decide.

## Completion report

After wave 2 verifies, report to the user:

- The two phase commits on `language-engine-seam` (hash + subject).
- The integration-gate result (build warnings, test totals).
- Each acceptance criterion from `task.md` and how it was proven.
- The combined deviation log from both Phase Reports.
- The standing flags (overview Open Questions): registration proven by convention until a gate
  consumes the seam; nothing calls the resolver yet.
- Reminder: nothing was pushed. After the human merges, they move this folder to
  `tasks/done/foundation/` and flip the `epics.md` **Language engine seam** checkbox to `[x]` — the
  plan skill never does.
