# in-process-compilation Orchestrator Runbook

**Trigger:** the user says "run in-process-compilation" (optionally "… starting at wave N"). If you
are the Claude session told this, follow this runbook exactly. It is the complete instruction set.
This runbook **drives** the phases; it never writes their production code itself.

## Ground rules (non-negotiable)

- **Waves.** Execute in **waves** (see `00-overview.md`, Execution waves). A wave starts only when
  every phase it depends on is committed on the task branch and verified.
- **Fresh context per phase.** Use one general-purpose subagent (Agent tool) per phase. Never run a
  phase's implementation in this orchestrating session, and never let one subagent touch two
  phases. The phase file is the entire handoff.
- **Parallel where the graph allows.** Wave 2 launches its three subagents **in a single message**
  so they run concurrently, each with `isolation: "worktree"`. Three is the cap.
- **Commits.** One commit per phase; the commit message references the phase file. Integrating
  wave 2 uses `git cherry-pick` of each phase commit in phase-number order. That preserves one
  commit per phase; it is not a squash, amend or rebase.
- **No history rewriting.** Never push to any remote; never amend, squash, rebase or force-push.
  Human review gates the push.
- **Branch.** Work happens on the **current branch** (expected: `in-process-compilation`, PR #22).
  If it is `main`, stop and ask the user first.
- **Preconditions:** the solution is restored; `dotnet build` / `dotnet test` are runnable; the
  `dotnet` SDK 11 is on PATH (the E2E suite runs `dotnet publish`; phase 5 runs a file-based app);
  network access to nuget.org for phase 1's new package. No database, no other services.

## Waves

| Wave | Phase | Phase file | Depends on | Mode |
|---|---|---|---|---|
| 1 | 1 | `tasks/todo/engine/in-process-compilation/01-compilation-contract.md` | — | single subagent, task branch |
| 2 | 2 | `tasks/todo/engine/in-process-compilation/02-project-build-discovery.md` | 1 | parallel, worktree |
| 2 | 3 | `tasks/todo/engine/in-process-compilation/03-compilation-references.md` | 1 | parallel, worktree |
| 2 | 4 | `tasks/todo/engine/in-process-compilation/04-roslyn-compiler.md` | 1 | parallel, worktree |
| 3 | 5 | `tasks/todo/engine/in-process-compilation/05-engine-compile.md` | 1, 2, 3, 4 | single subagent, task branch |

## Watchdog (whole run)

Before launching wave 1, arm the watchdog with the Monitor tool:

```
bash .claude/skills/epic-planning/watchdog.sh <subagents dir> <active-list file> <repo> 900
```

- **Arguments:**
  - `<subagents dir>` = `~/.claude/projects/C--Code-CodeWorker/<this session id>/subagents`
  - `<active-list file>` = a file in the scratchpad (start it empty)
  - `<repo>` = this checkout
- **Bookkeeping.** Append `agent-<agentId>.jsonl` to the active list when each subagent launches;
  remove its line when it reports. Re-arm the Monitor every time it expires. Stop it after the
  completion report.
- **`STALL` / `MISSING`.** Rule out a long build, restore or E2E publish (check the transcript
  tail), and message the subagent if it is stuck. If it is dead, apply **Halt on failure** (or
  relaunch the phase from a clean tree). `COMMIT` and `HEARTBEAT` are informational.

## Subagent prompt

Launch each phase's subagent with exactly this prompt (substitute the phase path and, for wave 2,
the wave base):

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

## Wave 1 — Phase 1 (single subagent)

1. Record `git rev-parse HEAD`.
2. Launch one general-purpose subagent (no worktree) with the prompt for Phase 1. Add it to the
   watchdog's active list. Wait for it.
3. **Verify:**
   - `git rev-list --count <recorded>..HEAD` is `1`;
   - `git status --porcelain` is empty;
   - the commit message references the phase file;
   - the subagent did not report PHASE FAILED;
   - the Phase Report names a resolved `Microsoft.CodeAnalysis.CSharp` version ≥ 5.12.0.
4. Pass → one-line status to the user (hash + subject). Fail → **Halt on failure**.

## Wave 2 — Phases 2, 3 and 4 (parallel worktrees)

1. Record the wave base: `git rev-parse HEAD` (Phase 1's commit).
2. In **one message**, launch three general-purpose subagents with `isolation: "worktree"`, one per
   phase. Add each to the watchdog's active list.
3. Right after launch, check each worktree's `HEAD`. One that is not on the wave base and is still
   clean: `git -C <worktree> reset --hard <wave base>`, then message that subagent to re-read what it
   read.
4. When all three have reported, verify **each** worktree independently:
   - `git -C <worktree> rev-list --count <wave base>..HEAD` is `1`
   - `git -C <worktree> status --porcelain` is empty
   - the commit message references its phase file; the subagent did not report PHASE FAILED
   - `git -C <worktree> show --stat HEAD` touches only its phase's `## Touches` (overview, wave-2
     file table)
5. Integrate onto the task branch **in phase order**: `git cherry-pick <phase 2>`, then
   `<phase 3>`, then `<phase 4>`.
6. **Integration gate:**
   - `dotnet build CodeWorker.slnx` with zero warnings;
   - `dotnet test CodeWorker.slnx` all green, including E2E;
   - `git status --porcelain` empty afterwards. If CSharpier changes anything on build, halt; do
     not commit a fix-up.
7. Clean up only after all three commits are on the task branch: `git worktree remove <path>` and
   `git branch -D <worktree branch>` for each.
8. Pass → one-line status per phase to the user. Any failure → **Halt on failure**.

## Wave 3 — Phase 5 (single subagent)

1. Record `git rev-parse HEAD`.
2. Launch one general-purpose subagent (no worktree) with the prompt for Phase 5. Add it to the
   watchdog's active list. Wait for it.
3. **Verify:**
   - `git rev-list --count <recorded>..HEAD` is `1`;
   - `git status --porcelain` is empty, and no `smoke.cs` is in the repo;
   - the commit message references the phase file;
   - the subagent did not report PHASE FAILED;
   - the Phase Report contains the smoke output (`Error = None`, zero error diagnostics, non-empty
     image) and both cold elapsed times;
   - `dotnet test CodeWorker.slnx` is green (re-run here, because this phase changes a
     constructor that two test classes build).
4. Pass → one-line status to the user. Fail → **Halt on failure**.

## Halt on failure

1. If the task-branch working tree is dirty, run
   `git stash push --include-untracked -m "in-process-compilation wave <n> failure"`.
   For a cherry-pick conflict, run `git cherry-pick --abort` first.
2. **Failure inside wave 2:** still integrate the phases that succeeded (they are independent).
   Prove them with build + test and remove their worktrees. Leave a failed phase's worktree in place
   for inspection. Never start wave 3: it depends on all three.
3. **Phase 5 smoke failure** naming a defect in phase 2, 3 or 4: record which phase owns it. The
   fix is a plan amendment to that phase plus a re-run, not a patch inside phase 5.
4. Write `tasks/todo/engine/in-process-compilation/failure-report.md` with:
   - the wave and phase(s);
   - what each subagent reported;
   - which verification check failed;
   - `git status` from before any stash, and the stash reference;
   - any worktree path left behind.
5. Stop. Report to the user and point at the failure report. Resume with
   "run in-process-compilation starting at wave N". Wave 2 resumes only the phases not yet on the
   branch; verify by commit subjects.

Special case: if a phase made more than one new commit, do not revert anything. Halt, report, and
let the human decide.

## Completion report

After wave 3 verifies, stop the watchdog and report to the user:

- The five phase commits on the task branch (hash + subject), in phase order.
- The resolved Roslyn package version (phase 1).
- The final suppressed-diagnostic list (phase 4).
- The smoke-run output and both cold elapsed times (phase 5), judged against the sub-10s budget.
- Each acceptance criterion from `task.md` and how it was proven.
- The combined deviation log from the five Phase Reports.
- The standing flags (overview Open Questions):
  - cold-start cost;
  - runtime vs reference pack;
  - framework-dependent multi-file publish requirement;
  - stale-helper type split;
  - no preprocessor symbols.
- Reminder: nothing was pushed. After the human merges, they move this folder to
  `tasks/done/engine/` and flip the `epics.md` **In-process compilation** checkbox to `[x]`. The
  plan skill never does.
