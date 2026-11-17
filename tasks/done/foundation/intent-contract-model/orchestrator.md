# intent-contract-model Orchestrator Runbook

**Trigger:** the user says "run intent-contract-model" (optionally "run intent-contract-model
starting at phase N"). If you are the Claude session told this, follow this runbook exactly — it is
the complete instruction set. This runbook **drives** the phases; it never writes their production
code itself.

## Ground rules (non-negotiable)

- Execute phases strictly in dependency order. The graph is a **chain** (1 → 2 → 3), so every wave
  holds one phase and there is no parallel step (see `00-overview.md`, Execution waves). Never start a
  phase before the previous one is verified complete.
- **Fresh context per phase.** Each phase runs in its own isolated context: one general-purpose
  subagent (Agent tool) per phase, working directly on the task branch (no worktree isolation is
  needed for a single-phase wave). Never execute a phase's implementation work in this orchestrating
  session, and never let one subagent touch two phases — the phase file is the entire handoff.
- One commit per phase; the commit message references the phase file.
- Never push to any remote; never amend, squash, rebase, or force-push — human review gates the push.
- Work happens on the **current branch** (expected: `intent-contract-model`). If the current branch
  is `main`, stop and ask the user first.
- **Preconditions:** a restored solution; `dotnet build` / `dotnet test` runnable. The E2E suite
  (`CodeWorker.Cli.EndToEnd.Tests`) runs `dotnet publish` on the CLI, so the `dotnet` SDK must be on
  PATH. No network services, no database.

## Phases (dependency order)

| # | Phase file | Depends on | Wave |
|---|---|---|---|
| 1 | `tasks/todo/foundation/intent-contract-model/01-intent-model-validation.md` | — | 1 |
| 2 | `tasks/todo/foundation/intent-contract-model/02-intent-file-parser.md` | 1 | 2 |
| 3 | `tasks/todo/foundation/intent-contract-model/03-wire-intent-into-verify.md` | 2 | 3 |

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
> squash existing commits, and never push to any remote. Report the commit hash, branch name, the
> Phase Report, and the working directory path when you finish.

## Per-phase procedure (repeat for phases 1, 2, 3 in order)

1. Record the current HEAD: `git rev-parse HEAD`.
2. Launch one general-purpose subagent with the subagent prompt above for the phase. Wait for it.
3. Verify in this session:
   - `git rev-list --count <recorded HEAD>..HEAD` is exactly `1`
   - `git status --porcelain` is empty
   - The new commit's message references the phase file
   - The subagent did not report PHASE FAILED
4. All checks pass → tell the user the phase is done (one line: commit hash + subject), keep its
   Phase Report for the completion report, and move to the next phase.
5. Any check fails → follow **Halt on failure** below.

## Halt on failure

If a phase reports PHASE FAILED, or its verification checks fail:

1. If the working tree is dirty, run
   `git stash push --include-untracked -m "intent-contract-model phase <n> failure"`.
2. Write `tasks/todo/foundation/intent-contract-model/failure-report.md` containing: the phase number
   and file, what the subagent reported, which verification check failed, the `git status` output from
   before any stash, and the stash reference if one was created.
3. Stop the pipeline. Never start a later phase — each depends on the one before. Report the failure
   to the user and point them at the failure report. They can resume with
   "run intent-contract-model starting at phase N" after fixing the cause (resuming at phase N
   requires phases 1..N-1 to already be committed on the branch — verify by their commit subjects).

Special case — more than one new commit for a phase: do not revert anything on your own; halt,
report, and let the human decide.

## Completion report

After Phase 3 verifies, report to the user:

- The three phase commits (hash + subject).
- Phase 3's manual smoke output and E2E results (valid intent prints the intent summary; malformed,
  non-observable, and class-mismatch intents each print their specific error; exit 0 in all cases —
  exit code is item 4).
- Each acceptance criterion from `task.md` and how it was proven.
- The combined deviation log gathered from the three phase reports (especially any change to the
  verb allow-list, message texts, precedence, or the parser's catch set).
- The standing flags for the human (overview Open Questions): invalid intent still exits 0 until item
  4; verb allow-list completeness; duplicate test names not checked; `CLAUDE.md` intent-file rule is
  an Integration item.
- Reminder: nothing was pushed; pushing is the human's call. After the human merges this item, they
  move the folder to `tasks/done/foundation/` and flip the `epics.md` **Intent contract model**
  checkbox to `[x]` — the plan skill never does.
