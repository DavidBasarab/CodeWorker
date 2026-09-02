# verify-command-surface Orchestrator Runbook

**Trigger:** the user says "run verify-command-surface" (optionally "run verify-command-surface
starting at phase N"). If you are the Claude session told this, follow this runbook exactly — it is
the complete instruction set. This runbook **drives** the phases; it never writes their production
code itself.

## Ground rules (non-negotiable)

- Execute phases strictly in dependency order: **1 → 2 → 3**. Phases 1 and 2 are independent of each
  other but both must complete before Phase 3; run them in the order listed. Never start a phase
  before the previous one is verified complete.
- **Fresh context per phase.** Each phase runs in its own isolated context: launch one
  general-purpose subagent (Agent tool) per phase, wait for it to finish, and verify its result
  before launching the next. Never execute a phase's implementation work in this orchestrating
  session, and never let one subagent touch two phases — the phase file is the entire handoff.
- One commit per phase; the commit message references the phase file.
- Never push to any remote; never amend, squash, rebase, or force-push — human review gates the push.
- Work happens on the **current branch** (expected: `CliTester`). If the current branch is `main`,
  stop and ask the user first.
- **Preconditions:** none beyond a restored solution — no MongoDB, no network, no external services.
  The whole item is local CLI code. `dotnet build` / `dotnet test` must be runnable.

## Phases (dependency order)

| # | Phase file | Depends on |
|---|---|---|
| 1 | `tasks/todo/foundation/verify-command-surface/01-command-seam.md` | — |
| 2 | `tasks/todo/foundation/verify-command-surface/02-argument-parser.md` | — |
| 3 | `tasks/todo/foundation/verify-command-surface/03-wire-verify-command.md` | 1, 2 |

## Per-phase procedure

1. Record the current HEAD: `git rev-parse HEAD`.
2. Launch a general-purpose subagent with exactly this prompt (substitute the phase path):

   > Execute the implementation phase described in `<phase path>`. Read that file completely first —
   > it is the entire handoff document; you have no other context. Follow every rule in CLAUDE.md
   > and .claude/rules. The phase file's Definition of Done, including the
   > unit-test-review → code-review → code-security-review loop, is mandatory. You may self-correct
   > at most 2 times; if the Definition of Done still cannot be met after that, leave the working
   > tree completely clean (discard or stash your changes), do NOT commit, and report PHASE FAILED
   > with an explanation. On success create exactly one commit whose message references
   > `<phase path>`. Never amend or squash existing commits, and never push to any remote.

3. When the subagent finishes, verify in this session:
   - `git rev-list --count <recorded HEAD>..HEAD` is exactly `1`
   - `git status --porcelain` is empty
   - The new commit's message references the phase file
   - The subagent did not report PHASE FAILED
4. All checks pass → tell the user the phase is done (one line: commit hash + subject) and move to
   the next phase.
5. Any check fails → follow **Halt on failure** below. Do not continue.

## Halt on failure

If a phase reports PHASE FAILED, or its verification checks fail:

1. If the working tree is dirty:
   `git stash push --include-untracked -m "verify-command-surface phase <n> failure"`.
2. Write `tasks/todo/foundation/verify-command-surface/failure-report.md` containing: the phase
   number and file, what the subagent reported, which verification check failed, the `git status`
   output from before the stash, and the stash reference if one was created.
3. Stop the pipeline. Do not start dependent phases. Report the failure to the user and point them
   at the failure report. They can resume with "run verify-command-surface starting at phase N"
   after fixing the cause.

Special case — more than one new commit for a phase: do not revert anything on your own; halt,
report, and let the human decide.

## Completion report

After Phase 3 verifies, report to the user:

- The three phase commits (hash + subject).
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
