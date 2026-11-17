# verdict-model-reporting Orchestrator Runbook

**Trigger:** the user says "run verdict-model-reporting" (optionally "… starting at wave N"). If you
are the Claude session told this, follow this runbook exactly — it is the complete instruction set.
This runbook **drives** the phases; it never writes their production code itself.

## Ground rules (non-negotiable)

- Execute in **waves** (see `00-overview.md`, Execution waves). A wave starts only when every phase
  it depends on is committed on the task branch. Never start a wave before the previous one is
  integrated and proven.
- **Fresh context per phase.** One general-purpose subagent (Agent tool) per phase; never run a
  phase's implementation in this orchestrating session; never let one subagent touch two phases —
  the phase file is the entire handoff.
- **Parallel where the graph allows.** A multi-phase wave launches all its subagents **in a single
  message** so they run concurrently, each with `isolation: "worktree"` (max 3 at once).
- **Worktree base.** `isolation: "worktree"` branches from `main`, not the task branch. Every
  parallel prompt names the wave base; right after launching, check each worktree's `HEAD` and move a
  clean one that is off-base with `git -C <worktree> reset --hard <wave base>`, then message its
  subagent to re-read what it read.
- One commit per phase; the commit message references the phase file. Integrating a parallel wave
  uses `git cherry-pick` of each phase commit in phase-number order — one commit per phase is
  preserved; this is not a squash, amend, or rebase.
- Never push to any remote; never amend, squash, rebase, or force-push — human review gates the push.
- Work happens on the **current branch** (expected: `verdict-model-reporting`). If it is `main`,
  stop and ask the user first.
- **Item wave.** `language-engine-seam` (item 5) may be running at the same time from a **separate
  checkout** (a `git worktree` of its own branch). Never switch this checkout's branch, and never
  touch `CodeWorker.Cli/Commands/Verify/Engines/`. When one session runs both items, keep the
  3-subagent cap: item 5's Phase 1 launches with this item's wave 2, its Phase 2 with wave 3 (see
  `../language-engine-seam/orchestrator.md`), and one watchdog covers every subagent.
- **Preconditions:** restored solution; `dotnet build` / `dotnet test` runnable; the `dotnet` SDK on
  PATH (the E2E suite runs `dotnet publish`). No network services, no database.

## Waves

| Wave | Phase | Phase file | Depends on | Mode |
|---|---|---|---|---|
| 1 | 1 | `tasks/todo/foundation/verdict-model-reporting/01-verdict-model-and-builder.md` | — | parallel, worktree |
| 1 | 2 | `tasks/todo/foundation/verdict-model-reporting/02-exit-codes.md` | — | parallel, worktree |
| 1 | 3 | `tasks/todo/foundation/verdict-model-reporting/03-verdict-flag.md` | — | parallel, worktree |
| 2 | 4 | `tasks/todo/foundation/verdict-model-reporting/04-verdict-json.md` | 1 | parallel, worktree |
| 2 | 5 | `tasks/todo/foundation/verdict-model-reporting/05-verdict-report.md` | 1 | parallel, worktree |
| 3 | 6 | `tasks/todo/foundation/verdict-model-reporting/06-deliver-verdict.md` | 1–5 | single subagent, task branch |

## Watchdog (whole run)

Before launching wave 1, arm the watchdog with the Monitor tool:

```
bash .claude/skills/epic-planning/watchdog.sh <subagents dir> <active-list file> <repo> 900
```

- `<subagents dir>` = `~/.claude/projects/C--Code-CodeWorker/<this session id>/subagents`;
  `<active-list file>` = a file in the scratchpad (start it empty); `<repo>` = this checkout.
- Append `agent-<agentId>.jsonl` to the active list when each subagent launches; remove its line when
  it reports. Re-arm the Monitor every time it expires. Stop it after the completion report.
- `STALL` / `MISSING`: rule out a long build or E2E publish (check the transcript tail), message the
  subagent if it is stuck; if it is dead, apply **Halt on failure** (or relaunch the phase from a
  clean tree). `COMMIT` and `HEARTBEAT` are informational.

## Subagent prompt

Launch each phase's subagent with exactly this prompt (substitute the phase path and, for a parallel
wave, the wave base):

> Execute the implementation phase described in `<phase path>`. Read that file completely first —
> it is the entire handoff document; you have no other context. **Parallel waves only:** your
> worktree was created from `main`; before reading any code run `git merge --ff-only <wave base>`
> and confirm `git rev-parse HEAD` equals `<wave base>`. Follow every rule in CLAUDE.md and
> .claude/rules. The phase file's Definition of Done, including the
> unit-test-review → code-review → code-security-review loop, is mandatory. Create or modify only
> the files the phase file's `## Touches` names — other phases may be running in parallel. You may
> self-correct at most 2 times; if the Definition of Done still cannot be met after that, leave the
> working tree completely clean (discard or stash your changes), do NOT commit, and report PHASE
> FAILED with an explanation. On success create exactly one commit, on the branch your working
> directory is already on, whose message references `<phase path>`. Never switch branches, never
> amend or squash existing commits, and never push to any remote. Report the commit hash, branch
> name, the Phase Report, and the working directory path when you finish.

## Parallel wave procedure (waves 1 and 2)

1. Record the wave base: `git rev-parse HEAD`.
2. In **one message**, launch one general-purpose subagent per phase with `isolation: "worktree"`
   (wave 1: Phases 1, 2, 3; wave 2: Phases 4, 5). Add each to the watchdog's active list.
3. Check each worktree's `HEAD` against the wave base (Worktree base rule above).
4. When all have reported, verify **each** worktree independently:
   - `git -C <worktree> rev-list --count <wave base>..HEAD` is `1`
   - `git -C <worktree> status --porcelain` is empty
   - the commit message references its phase file; the subagent did not report PHASE FAILED
   - `git -C <worktree> show --stat HEAD` touches only its phase's `## Touches`
5. Integrate onto the task branch **in phase order** with `git cherry-pick <commit>`.
6. **Integration gate:** `dotnet build CodeWorker.slnx` (zero warnings) and `dotnet test CodeWorker.slnx`
   (all green, including E2E). `git status --porcelain` empty afterwards — if CSharpier changes
   anything on build, halt; do not commit a fix-up.
7. Clean up only after every commit is on the task branch: `git worktree remove <path>` and
   `git branch -D <worktree branch>` for each.
8. Pass → one-line status per phase to the user. Any failure → **Halt on failure**.

## Wave 3 — Phase 6 (single subagent)

1. Record `git rev-parse HEAD`.
2. Launch one general-purpose subagent (no worktree) with the prompt for Phase 6; add it to the
   watchdog's active list. Wait for it.
3. Verify: `git rev-list --count <recorded>..HEAD` is `1`; `git status --porcelain` is empty; the
   commit message references the phase file; no PHASE FAILED; `dotnet test CodeWorker.slnx` green
   (re-run here — this phase changes exit codes the E2E suite asserts).
4. Pass → one-line status to the user. Fail → **Halt on failure**.

## Halt on failure

1. If the task-branch working tree is dirty, run
   `git stash push --include-untracked -m "verdict-model-reporting wave <n> failure"`.
   A cherry-pick conflict: `git cherry-pick --abort` first.
2. **Failure inside a parallel wave:** still integrate the phases that succeeded (they are
   independent), prove them with the integration gate, and remove their worktrees; leave the failed
   phase's worktree in place for inspection. Do not start a wave whose dependencies include the
   failed phase (Phase 1 failing blocks waves 2 and 3; Phase 2 or 3 failing blocks only wave 3).
3. Write `tasks/todo/foundation/verdict-model-reporting/failure-report.md`: the wave and phase(s),
   what each subagent reported, which verification check failed, `git status` from before any stash,
   the stash reference, and any worktree path left behind.
4. Stop the watchdog. Report to the user and point at the failure report. Resume with
   "run verdict-model-reporting starting at wave N" (a parallel wave resumes only the phases not yet
   on the branch — verify by commit subjects).

Special case — more than one new commit for a phase: do not revert anything; halt, report, and let
the human decide.

## Completion report

After wave 3 verifies, report to the user:

- The six phase commits on the task branch (hash + subject), in phase order.
- Phase 6's manual smoke output (green verdict file, `Verdict: GREEN — No gate failed.`, exit 0;
  malformed intent exits 2) and the integrated E2E result.
- Each acceptance criterion from `task.md` and how it was proven.
- The combined deviation log from the six Phase Reports.
- The standing flags (overview Open Questions): report log level for blocked; the item-3 summary log
  line kept; a bad `--verdict` path exits 3.
- Reminder: nothing was pushed. After the human merges, they move this folder to
  `tasks/done/foundation/` and flip the `epics.md` **Verdict model + reporting** checkbox to `[x]` —
  the plan skill never does.
