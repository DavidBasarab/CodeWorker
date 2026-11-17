# intent-contract-model — Failure Report

- **Date:** 2026-10-14
- **Failed phase:** 2 — `02-intent-file-parser.md`
- **Completed before it:** Phase 1 — `e3a773b` (intent model + contract validator), verified.
- **Not started:** Phase 3 (depends on Phase 2).

## What the subagent reported

PHASE FAILED — the review loop did not come back clean within the 2 self-corrections allowed. The
code itself built and passed its tests: TDD was red then green, with 33 new tests and 625/625
passing across the solution. `unit-test-review` passed after one correction round, but
`code-review` still had findings after the second round, and `code-security-review` was never
reached.

## Which check failed

The Definition of Done review loop, at the `code-review` step. **Root cause: the plan was wrong.**

- ADR-5 and test 14 assume `IFileSystemTools.ReadAllText` throws `FileNotFoundException` for a
  missing file.
- In fact it returns `string.Empty` (`C:\Code\FatCat.Toolkit\src\ToolKit\FileSystemTools.cs`,
  `ReadAllText` → `FileDoesNotExist(path) ? string.Empty : ...`; verified by the orchestrator).
- So a vanished intent file surfaces as `MalformedIntentJson`, and plan test 14 fakes an exception
  that can never happen.
- The subagent's fix attempt added a `FileExists` guard and `AllowDuplicateProperties = false`.
  Neither is in the plan, and code-review flagged both: the guard duplicates item 1's check and is
  racy; the duplicate-property rule is unplanned scope.

Remaining code-review findings at the halt:
1. Missing-file race: the file still becomes `""` and is reported as `MalformedIntentJson`.
2. Test 14 models an impossible exception.
3. The `FileExists` guard duplicates `ParseVerifyArguments`' `IntentFileNotFound` (two codes for one
   condition).
4. A directory path is reported as "file does not exist".
5. `AllowDuplicateProperties = false` goes beyond the design and interacts with case-insensitive
   matching.
6. `ParseIntentTests.cs` has a redundant `using System.IO;`.

## State

- `git status` before stash: the subagent stashed its own work; the tree was clean at hand-back.
- Stash: `stash@{0}` — "intent-contract-model phase 2 (PHASE FAILED - review loop not clean)
  ParseIntent" (includes untracked `ParseIntent.cs` and `ParseIntentTests.cs`).
- HEAD: `e3a773b` on `intent-contract-model`. No worktrees. Nothing pushed.
- Review report: `.reviews/2026-10-14-131748-tests-parse-intent.md` (gitignored).

## Plan decisions needed before a retry

1. How to handle a missing file given that the toolkit returns `""` (see orchestrator
   recommendation to the user).
2. Duplicate JSON properties: reject or allow.
3. Strict JSON (no comments or trailing commas): keep as ADR-5 states.
4. Drop the redundant `using`.

Resume with "run intent-contract-model starting at phase 2" after amending
`02-intent-file-parser.md` and `00-overview.md` ADR-5.

## Resolution (2026-10-14)

User decisions: empty/whitespace content → `IntentFileUnreadable` (no `FileExists` guard); duplicate
properties allowed (last wins); strict JSON kept. `02-intent-file-parser.md` and overview ADR-5
amended accordingly; the failed attempt's stash was dropped and Phase 2 re-run from the amended plan.
