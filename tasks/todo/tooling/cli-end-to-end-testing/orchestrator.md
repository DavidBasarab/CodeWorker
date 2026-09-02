# cli-end-to-end-testing Orchestrator Runbook

**Trigger:** the user says "run cli-end-to-end-testing" (optionally "run cli-end-to-end-testing
starting at phase N"). If you are the Claude session told this, follow this runbook exactly — it is the
complete instruction set. This runbook **drives** the phases; it never writes their test code itself.

## Ground rules (non-negotiable)

- Execute phases strictly in dependency order: **1 → 2 → 3**. Phases 2 and 3 both depend on Phase 1
  and are independent of each other; run them in the listed order. Never start a phase before the
  previous one is verified complete.
- **Fresh context per phase.** Each phase runs in its own isolated context: launch one general-purpose
  subagent (Agent tool) per phase, wait for it to finish, and verify its result before launching the
  next. Never execute a phase's implementation work in this orchestrating session, and never let one
  subagent touch two phases — the phase file is the entire handoff.
- One commit per phase; the commit message references the phase file.
- Never push to any remote; never amend, squash, rebase, or force-push — human review gates the push.
- Work happens on the **current branch** (expected: `CliTester`). If the current branch is `main`,
  stop and ask the user first.
- **Preconditions:** a restored solution and the .NET 10 SDK on PATH (the publish fixture shells out to
  `dotnet publish`). No MongoDB, no network, no external services — the whole item is local test code.

## Phases (dependency order)

| # | Phase file | Depends on |
|---|---|---|
| 1 | `tasks/todo/tooling/cli-end-to-end-testing/01-harness-spine.md` | — |
| 2 | `tasks/todo/tooling/cli-end-to-end-testing/02-valid-invocation.md` | 1 |
| 3 | `tasks/todo/tooling/cli-end-to-end-testing/03-usage-errors.md` | 1 |

## Per-phase procedure

1. Record the current HEAD: `git rev-parse HEAD`.
2. Launch a general-purpose subagent with exactly this prompt (substitute the phase path):

   > Execute the implementation phase described in `<phase path>`. Read that file completely first —
   > it is the entire handoff document; you have no other context. Follow every rule in CLAUDE.md and
   > .claude/rules. The phase file's Definition of Done, including the
   > unit-test-review → code-review → code-security-review loop, is mandatory. You may self-correct at
   > most 2 times; if the Definition of Done still cannot be met after that, leave the working tree
   > completely clean (discard or stash your changes), do NOT commit, and report PHASE FAILED with an
   > explanation. On success create exactly one commit whose message references `<phase path>`. Never
   > amend or squash existing commits, and never push to any remote.

3. When the subagent finishes, verify in this session:
   - `git rev-list --count <recorded HEAD>..HEAD` is exactly `1`
   - `git status --porcelain` is empty
   - The new commit's message references the phase file
   - The subagent did not report PHASE FAILED
   - `dotnet test CodeWorker.sln --filter "Category=EndToEnd"` is green
   - `dotnet test CodeWorker.sln --filter "Category!=EndToEnd"` is green (the fast unit loop still
     passes and did not trigger a publish)
4. All checks pass → tell the user the phase is done (one line: commit hash + subject) and move to the
   next phase.
5. Any check fails → follow **Halt on failure** below. Do not continue.

## Halt on failure

If a phase reports PHASE FAILED, or its verification checks fail:

1. If the working tree is dirty:
   `git stash push --include-untracked -m "cli-end-to-end-testing phase <n> failure"`.
2. Write `tasks/todo/tooling/cli-end-to-end-testing/failure-report.md` containing: the phase number and
   file, what the subagent reported, which verification check failed, the `git status` output from
   before the stash, and the stash reference if one was created.
3. Stop the pipeline. Do not start dependent phases. Report the failure to the user and point them at
   the failure report. They can resume with "run cli-end-to-end-testing starting at phase N" after
   fixing the cause.

Special case — more than one new commit for a phase: do not revert anything on your own; halt, report,
and let the human decide.

## Completion report

After Phase 3 verifies, report to the user:

- The three phase commits (hash + subject).
- The E2E results: the banner smoke test, the green-path parse output, and each usage-error message
  (missing flag ×3, missing file ×3, no-arguments), plus confirmation that every case exits 0 (the
  ADR-3 tripwire).
- The two filter invocations and their counts: `--filter "Category=EndToEnd"` (the E2E suite) and
  `--filter "Category!=EndToEnd"` (the unchanged fast unit suite).
- Each acceptance criterion from `task.md` and how it was proven.
- The combined deviation log from the three phase reports — especially any asserted message fragment
  that had to be adjusted to match the real rendered Serilog line, the resolved published exe path, and
  the measured one-time publish cost.
- The standing flags for the human (overview Open Questions): exit-0 is pinned to today's behavior and
  these assertions must be updated when the **Verdict model + reporting** item wires a non-zero exit
  (ADR-3); the fixture publishes `-c Release` (ADR-2, changeable); this is standalone tooling, so no
  `epics.md` checkbox is flipped.
- Reminder: nothing was pushed; pushing is the human's call.
