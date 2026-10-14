# verify-command-surface Orchestrator Runbook

**Trigger:** the user says "run verify-command-surface" (optionally "run verify-command-surface
starting at phase N"). If you are the Claude session told this, follow this runbook exactly — it is
the complete instruction set. This runbook **drives** the phases; it never writes their production
code itself.

## Ground rules (non-negotiable)

- Execute phases in **waves** by dependency order, using parallel subagents wherever the dependency
  graph allows (see `.claude/skills/epic-planning/SKILL.md` — Parallel execution rule):
  - **Wave 1 — Phases 1 and 2 in parallel.** They are independent and touch disjoint files
    (see the Execution waves table in `00-overview.md`).
  - **Wave 2 — Phase 3**, only after both wave-1 commits are integrated and verified.
  Never start a wave before the previous wave is verified complete.
- **Fresh context per phase.** Each phase runs in its own isolated context: one general-purpose
  subagent (Agent tool) per phase. Never execute a phase's implementation work in this orchestrating
  session, and never let one subagent touch two phases — the phase file is the entire handoff.
- **Parallel phases each get their own git worktree** (`isolation: "worktree"` on the Agent call) so
  concurrent builds, tests, reviews, and commits never collide.
- One commit per phase; the commit message references the phase file. Integrating a worktree commit
  with `git cherry-pick` preserves that one-commit-per-phase boundary.
- Never push to any remote; never amend, squash, rebase, or force-push — human review gates the push.
- Work happens on the **current branch** (expected: `CliTester`). If the current branch is `main`,
  stop and ask the user first.
- **Preconditions:** none beyond a restored solution — no MongoDB, no network, no external services.
  The whole item is local CLI code. `dotnet build` / `dotnet test` must be runnable.

## Phases (dependency order)

| # | Phase file | Depends on | Wave |
|---|---|---|---|
| 1 | `tasks/todo/foundation/verify-command-surface/01-command-seam.md` | — | 1 (parallel) |
| 2 | `tasks/todo/foundation/verify-command-surface/02-argument-parser.md` | — | 1 (parallel) |
| 3 | `tasks/todo/foundation/verify-command-surface/03-wire-verify-command.md` | 1, 2 | 2 |

## Subagent prompt

Launch each phase's subagent with exactly this prompt (substitute the phase path):

> Execute the implementation phase described in `<phase path>`. Read that file completely first —
> it is the entire handoff document; you have no other context. Follow every rule in CLAUDE.md
> and .claude/rules. The phase file's Definition of Done, including the
> unit-test-review → code-review → code-security-review loop, is mandatory. You may self-correct
> at most 2 times; if the Definition of Done still cannot be met after that, leave the working
> tree completely clean (discard or stash your changes), do NOT commit, and report PHASE FAILED
> with an explanation. On success create exactly one commit, on the branch your working directory
> is already on, whose message references `<phase path>`. Never switch branches, never amend or
> squash existing commits, and never push to any remote. Report the commit hash, branch name, and
> working directory path when you finish.

## Wave 1 procedure — Phases 1 and 2 in parallel

1. Record the wave base: `git rev-parse HEAD`.
2. In **a single message**, launch two general-purpose subagents — one for Phase 1, one for
   Phase 2 — each with `isolation: "worktree"` and the subagent prompt above. Wait for both.
3. Verify each phase in its worktree (path and branch come from the subagent/tool result):
   - `git rev-list --count <wave base>..<worktree branch>` is exactly `1`
   - `git -C <worktree path> status --porcelain` is empty
   - The commit message references that phase's file
   - The subagent did not report PHASE FAILED
4. Integrate onto the task branch in phase-number order — Phase 1's commit, then Phase 2's:
   `git cherry-pick <phase 1 commit>` then `git cherry-pick <phase 2 commit>`.
5. Prove the combination (each phase was only proven alone):
   - `dotnet build CodeWorker.sln` — zero warnings
   - `dotnet test CodeWorker.sln` — all tests pass
   - `git rev-list --count <wave base>..HEAD` is exactly `2`; `git status --porcelain` is empty
6. Remove the two worktrees and their branches (`git worktree remove <path>`,
   `git branch -D <branch>`) — only after both commits are on the task branch.
7. All checks pass → tell the user wave 1 is done (one line per phase: commit hash + subject) and
   move to wave 2.
8. Any check fails → follow **Halt on failure** below. Do not start wave 2.

## Wave 2 procedure — Phase 3

1. Record the current HEAD: `git rev-parse HEAD`.
2. Launch one general-purpose subagent (no worktree isolation — it works directly on the task
   branch) with the subagent prompt above for Phase 3.
3. When it finishes, verify in this session:
   - `git rev-list --count <recorded HEAD>..HEAD` is exactly `1`
   - `git status --porcelain` is empty
   - The new commit's message references the phase file
   - The subagent did not report PHASE FAILED
4. All checks pass → tell the user the phase is done (one line: commit hash + subject) and produce
   the completion report.
5. Any check fails → follow **Halt on failure** below.

## Halt on failure

If a phase reports PHASE FAILED, or its verification checks fail:

1. **Wave 1 only — keep what succeeded.** Phases 1 and 2 are independent, so cherry-pick the phase
   that passed verification onto the task branch (then run the step-5 build/test check). Leave the
   failed phase's worktree in place for inspection; do not cherry-pick from it.
2. If the task branch's working tree is dirty (e.g. a cherry-pick conflict — run
   `git cherry-pick --abort` first), run
   `git stash push --include-untracked -m "verify-command-surface phase <n> failure"`.
3. Write `tasks/todo/foundation/verify-command-surface/failure-report.md` containing: the phase
   number(s) and file(s), what each subagent reported, which verification check failed, the
   `git status` output from before any stash, the stash reference if one was created, and the path
   and branch of any worktree left in place.
4. Stop the pipeline. Phase 3 depends on both 1 and 2 — never start it if either failed. Report the
   failure to the user and point them at the failure report. They can resume with
   "run verify-command-surface starting at phase N" after fixing the cause (resuming at phase 1 or 2
   runs only the phase(s) not yet on the task branch; resuming at phase 3 requires both wave-1
   commits to be present).

Special case — more than one new commit for a phase, or a worktree branch not based on the wave
base: do not revert or cherry-pick anything on your own; halt, report, and let the human decide.

## Completion report

After Phase 3 verifies, report to the user:

- The three phase commits (hash + subject), noting that Phases 1 and 2 ran in parallel.
- The end-to-end CLI smoke results from Phase 3 (valid invocation prints the parsed paths; missing
  flag, missing file, and no-arguments each print their specific usage error; process exits 0 in all
  cases — exit code is item 4).
- Each acceptance criterion from `task.md` and how it was proven.
- The combined deviation log gathered from the three phase reports (especially any adaptation to the
  real `IFileSystemTools.FileExists` signature, and confirmation that `CommandResolver`'s duplicated
  switch arms were kept as the intentional extension seam).
- The standing flags for the human (overview Open Questions): a malformed invocation currently logs
  a usage error but exits 0 until item 4 wires the non-zero exit (ADR-5); unknown-verb specificity
  and a help command are deferred until a second command exists (ADR-2); single vs. multiple files
  per invocation (ADR-3).
- Reminder: nothing was pushed; pushing is the human's call. After the human merges this item, they
  flip the `epics.md` **Verify command surface** checkbox to `[x]` — the plan skill never does.
