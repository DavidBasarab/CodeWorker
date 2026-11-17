# gate-pipeline-framework Orchestrator Runbook

**Trigger:** the user says "run gate-pipeline-framework" (optionally "… starting at wave N"). If you
are the Claude session told this, follow this runbook exactly — it is the complete instruction set.
This runbook **drives** the phases; it never writes their production code itself.

## Ground rules (non-negotiable)

- Execute in **waves** (see `00-overview.md`, Execution waves). A wave starts only when every phase
  it depends on is committed on the task branch. Never start wave 2 before wave 1 is verified.
- **Fresh context per phase.** One general-purpose subagent (Agent tool) per phase; never run a
  phase's implementation in this orchestrating session; never let one subagent touch two phases —
  the phase file is the entire handoff.
- **Parallel where the graph allows.** A multi-phase wave launches all its subagents **in a single
  message** so they run concurrently, each with `isolation: "worktree"`.
- One commit per phase; the commit message references the phase file. Integrating a parallel wave
  uses `git cherry-pick` of each phase commit in phase-number order — one commit per phase is
  preserved; this is not a squash, amend, or rebase.
- Never push to any remote; never amend, squash, rebase, or force-push — human review gates the push.
- Work happens on the **current branch** (expected: `gate-pipeline-framework`). If it is `main`,
  stop and ask the user first.
- **Preconditions:** restored solution; `dotnet build` / `dotnet test` runnable; the `dotnet` SDK on
  PATH (the E2E suite runs `dotnet publish`). No network services, no database.

## Waves

| Wave | Phase | Phase file | Depends on | Mode |
|---|---|---|---|---|
| 1 | 1 | `tasks/todo/foundation/gate-pipeline-framework/01-gate-contract-and-runner.md` | — | single subagent, task branch |
| 2 | 2 | `tasks/todo/foundation/gate-pipeline-framework/02-gate-timing.md` | 1 | parallel, worktree |
| 2 | 3 | `tasks/todo/foundation/gate-pipeline-framework/03-wire-pipeline-into-verify.md` | 1 | parallel, worktree |

## Subagent prompt

Launch each phase's subagent with exactly this prompt (substitute the phase path):

> Execute the implementation phase described in `<phase path>`. Read that file completely first —
> it is the entire handoff document; you have no other context. Follow every rule in CLAUDE.md
> and .claude/rules. The phase file's Definition of Done, including the
> unit-test-review → code-review → code-security-review loop, is mandatory. Create or modify only
> the files the phase file names — other phases may be running in parallel. You may self-correct
> at most 2 times; if the Definition of Done still cannot be met after that, leave the working
> tree completely clean (discard or stash your changes), do NOT commit, and report PHASE FAILED
> with an explanation. On success create exactly one commit, on the branch your working directory
> is already on, whose message references `<phase path>`. Never switch branches, never amend or
> squash existing commits, and never push to any remote. Report the commit hash, branch name, the
> Phase Report, and the working directory path when you finish.

## Wave 1 — Phase 1 (single subagent)

1. Record `git rev-parse HEAD`.
2. Launch one general-purpose subagent (no worktree) with the prompt for Phase 1. Wait for it.
3. Verify: `git rev-list --count <recorded>..HEAD` is `1`; `git status --porcelain` is empty; the
   commit message references the phase file; no PHASE FAILED.
4. Pass → one-line status to the user (hash + subject); keep the Phase Report. Fail → **Halt on failure**.

## Wave 2 — Phases 2 and 3 (parallel worktrees)

1. Record the wave base: `git rev-parse HEAD` (Phase 1's commit).
2. In **one message**, launch two general-purpose subagents with `isolation: "worktree"` — one with
   the prompt for Phase 2, one for Phase 3. Wait for both.
3. Verify **each** worktree independently:
   - `git -C <worktree> rev-list --count <wave base>..HEAD` is `1`
   - `git -C <worktree> status --porcelain` is empty
   - the commit message references its phase file; the subagent did not report PHASE FAILED
   - the commit touches only its phase's files (`git -C <worktree> show --stat HEAD` against the
     overview's wave-2 file table)
4. Integrate the verified commits onto the task branch **in phase order**:
   `git cherry-pick <phase 2 commit>`, then `git cherry-pick <phase 3 commit>`.
5. Prove the combination (each phase was only proven alone — Phase 3's E2E ran without the clock):
   `dotnet build CodeWorker.slnx` (zero warnings) and `dotnet test CodeWorker.slnx` (all green,
   including E2E). `git status --porcelain` empty afterwards (CSharpier on build must not change
   anything; if it does, halt — do not commit a fix-up).
6. Clean up only after both commits are on the task branch: `git worktree remove <path>` and
   `git branch -D <worktree branch>` for each.
7. Pass → one-line status per phase to the user. Any failure → **Halt on failure**.

## Halt on failure

1. If the task-branch working tree is dirty, run
   `git stash push --include-untracked -m "gate-pipeline-framework wave <n> failure"`.
   A cherry-pick conflict: `git cherry-pick --abort` first.
2. **Failure inside wave 2:** still integrate the phase that succeeded (the two are independent),
   prove it with build + test, and keep its worktree removed; leave the failed phase's worktree in
   place for inspection.
3. Write `tasks/todo/foundation/gate-pipeline-framework/failure-report.md`: the wave and phase(s),
   what each subagent reported, which verification check failed, `git status` from before any
   stash, the stash reference, and any worktree path left behind.
4. Stop. Report to the user and point at the failure report. Resume with
   "run gate-pipeline-framework starting at wave N" (wave 2 resumes only the phases not yet on the
   branch — verify by commit subjects).

Special case — more than one new commit for a phase: do not revert anything; halt, report, and let
the human decide.

## Completion report

After wave 2 verifies, report to the user:

- The three phase commits on the task branch (hash + subject), in phase order.
- Phase 3's manual smoke output and the integrated E2E result
  (`verify: gate pipeline finished with 0 gates, blocked: False`; exit 0).
- Each acceptance criterion from `task.md` and how it was proven.
- The combined deviation log from the three Phase Reports.
- The standing flags (overview Open Questions): `IClock` duplicated in the CLI and main project;
  `types-and-di.md` scanning wording; block-vs-warn still decided per gate until Integration;
  exit code still 0 until item 4.
- Reminder: nothing was pushed. After the human merges, they move this folder to
  `tasks/done/foundation/` and flip the `epics.md` **Gate pipeline framework** checkbox to `[x]` —
  the plan skill never does.
