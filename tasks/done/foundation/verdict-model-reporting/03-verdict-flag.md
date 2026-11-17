# Phase 3 — `--verdict <path>` Flag

- **Work item:** verdict-model-reporting (see `tasks/todo/foundation/verdict-model-reporting/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 6
- **Wave:** 1
- **Parallel with:** Phase 1, Phase 2 — no dependency, disjoint write sets, no shared hotspot
- **Risk:** **High** (auto: public CLI contract) — adds a flag the `Stop` hook will depend on and
  changes the usage line every caller sees.

## Touches

- Creates: nothing
- Modifies: `CodeWorker.Cli/Commands/Verify/ParseVerifyArguments.cs`,
  `CodeWorker.Cli/Commands/Verify/VerifyArgumentsResult.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/ParseVerifyArgumentsTests.cs`
- Exclusive runtime resources: none

Other phases run at the same time in their own worktrees. Create or modify **only** these files.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("Usage"), `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md`
first — mandatory.

**Worktree base:** before reading code, run `git merge --ff-only <wave base>` (the commit the
orchestrator names) in your still-clean worktree, then confirm `git rev-parse HEAD` equals it.

Current state:
- `ParseVerifyArguments(IFileSystemTools fileSystemTools) : IParseVerifyArguments` —
  `VerifyArgumentsResult Parse(string[] args)`. Constants `IntentFlag`, `ProductionFlag`, `TestsFlag`,
  `UsageLine = "Usage: verify --intent <intent.json> --production <Foo.cs> --tests <FooTests.cs>"`.
  Checks (in order): any known flag present → `--intent` → `--production` → `--tests` values present
  → each of the three files exists (`fileSystemTools.FileExists`). Private static
  `TryGetFlagValue(args, flag, out value)` returns the first occurrence followed by a non-flag token.
  `ContainsAnyFlag` checks the three flags.
- `VerifyArgumentsResult { IsValid; Error; Message; IntentPath; ProductionPath; TestsPath }`.
- `ParseVerifyArgumentsTests` — fakes `IFileSystemTools` (`FileExists` → true by default);
  `WellFormedArgs()` helper returns `["verify", "--intent", "intent.json", "--production", "Foo.cs", "--tests", "FooTests.cs"]`.
- E2E usage tests assert only the prefix `"Usage: verify --intent"` — appending to the usage line is safe.

## Design (build exactly this shape)

- `VerifyArgumentsResult` gains `public string VerdictPath { get; set; }`.
- `ParseVerifyArguments` gains `private const string VerdictFlag = "--verdict";` and, on the success
  path only, reads it with `TryGetFlagValue(args, VerdictFlag, out var verdictPath)`; set
  `VerdictPath = verdictPath` (null when absent or valueless). It is **never** passed to `FileExists`.
- `--verdict` alone does **not** count for `ContainsAnyFlag` — the three required flags still decide
  "no arguments". Leave that method unchanged.
- `UsageLine` becomes
  `"Usage: verify --intent <intent.json> --production <Foo.cs> --tests <FooTests.cs> [--verdict <verdict.json>]"`.
- No other validation changes; error precedence is unchanged.

## Steps (TDD — tests first, red before green)

Add to `ParseVerifyArgumentsTests` (one assertion each; existing tests stay green unchanged):

1. `ReturnTheVerdictPathWhenGiven` — `WellFormedArgs()` + `"--verdict", "verdict.json"` → `VerdictPath` is `"verdict.json"`.
2. `ReturnNoVerdictPathWhenAbsent` — `WellFormedArgs()` → `VerdictPath` is null.
3. `ReturnNoVerdictPathWhenItHasNoValue` — `WellFormedArgs()` + trailing `"--verdict"` → null.
4. `StayValidWithAVerdictFlag` — `IsValid` true.
5. `NotCheckThatTheVerdictFileExists` — `FileExists("verdict.json")` `MustNotHaveHappened`.
6. `AcceptTheVerdictFlagBeforeTheOtherFlags` — `--verdict` first in the args → path still read.
7. `MentionTheVerdictFlagInTheUsage` — no-arguments result `Message` contains `"[--verdict <verdict.json>]"`.

Red first, then implement to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass, including the E2E usage tests unchanged
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] No banned patterns; string interpolation; no expression-bodied members
- [ ] Only the files in `## Touches` created/modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review` (screen-only — write nothing outside `## Touches`)
- [ ] Exactly one commit on your worktree branch, message referencing this file; no push

Suggested commit message:

```
verdict-model-reporting phase 3: --verdict flag (tasks/todo/foundation/verdict-model-reporting/03-verdict-flag.md)
```

## Rollback Procedure

- Revert Phase 6 first if committed, then `git revert <this commit>`. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts; deviation log (an empty log is a claim, not a default);
open questions/risks.

## Hand-off

- `VerifyArgumentsResult.VerdictPath` — the verdict file path or null. Phase 6 passes it to
  `IDeliverVerdict.Deliver`.
- The usage line now ends with `[--verdict <verdict.json>]`.
