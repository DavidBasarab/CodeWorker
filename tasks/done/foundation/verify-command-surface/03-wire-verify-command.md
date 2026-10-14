# Phase 3 — Wire the Parser into `VerifyCommand` + Usage Reporting + End-to-End Smoke

- **Work item:** verify-command-surface (see
  `tasks/todo/foundation/verify-command-surface/00-overview.md`)
- **Depends on:** Phase 1 (`01-command-seam.md`) — the `VerifyCommand` / `IRunVerifyCommand` seam;
  Phase 2 (`02-argument-parser.md`) — `IParseVerifyArguments` + `VerifyArgumentsResult`
- **Depended on by:** —
- **Risk:** **Low** — connects two already-tested pieces and logs the usage error. No auth/network/
  data surface. Note it deliberately does **not** set the process exit code (overview ADR-5).

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Pay special attention to `errors-and-logging.md` (log at the action site; usage errors
are values; `ILogger` injected via constructor) and `testing.md`.

This phase makes `verify` real: `VerifyCommand` now parses its args and, on failure, logs the
specific usage error; on success it holds the valid `VerifyArgumentsResult` for later Foundation
items. It closes the acceptance criteria for this work item.

Current state you will find:

- Phase 1: `VerifyCommand(ILogger logger) : IRunVerifyCommand` — skeleton whose `Execute` logs
  `"Verify command invoked"` at Debug and returns `Task.CompletedTask`. You will **replace its body
  and constructor**.
- Phase 2: `IParseVerifyArguments.Parse(string[] args)` returns a `VerifyArgumentsResult` with
  `IsValid`, a tagged `VerifyUsageError`, a human-readable `Message`, and the three paths.
- `ProcessArguments` already resolves `verify` to this command and calls `Execute(args)` with the
  full args array (including `args[0] == "verify"`), which the parser handles.

### What this phase does NOT do (scope guard — later Foundation items)

- It does not parse the intent file contents (item 2), run any gate (item 3), or emit a
  verdict/report or set a non-zero process exit code (item 4). On a usage error the process **still
  exits 0** — the failure is expressed as the logged value (overview ADR-5). Do not wire
  `Environment.ExitCode` or change `Program.Main`'s signature.

## Design (build exactly this shape)

**`CodeWorker.Cli/Commands/Verify/VerifyCommand.cs`** — inject the parser; keep the capability
interface. Log the usage error on failure; on success, do nothing further this item (the valid
result is the contract later items consume). Block body returning `Task.CompletedTask` — still no
awaitable work, so no `async` keyword (avoids CS1998):

```csharp
using FatCat.CodeWorker.Cli.Commands;
using Serilog;

namespace FatCat.CodeWorker.Cli.Commands.Verify;

public interface IRunVerifyCommand : ICommand { }

public class VerifyCommand(IParseVerifyArguments parseVerifyArguments, ILogger logger) : IRunVerifyCommand
{
	public Task Execute(string[] args)
	{
		var result = parseVerifyArguments.Parse(args);

		if (!result.IsValid)
		{
			logger.Error("verify: {UsageError}", result.Message);

			return Task.CompletedTask;
		}

		logger.Information(
			"verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}",
			result.IntentPath,
			result.ProductionPath,
			result.TestsPath
		);

		return Task.CompletedTask;
	}
}
```

Notes:
- Log the parser's `Message` (already specific and human-readable). Do **not** rebuild the message
  here — the parser owns it.
- The success log is a breadcrumb only; later items replace it with the real pipeline call. Keep it
  so the walking skeleton visibly does something end to end.
- Injecting `IParseVerifyArguments` and `ILogger` is auto-resolved by scanning — no module change.

## Steps (TDD — tests first, red before green)

Rewrite `CodeWorker.Cli.Tests/Commands/Verify/VerifyCommandTests.cs` (namespace
`Testing.FatCat.CodeWorker.Cli.Commands.Verify`). Constructor: fake `IParseVerifyArguments` and
`ILogger`; use a settable field for the returned result —
`A.CallTo(() => parseVerifyArguments.Parse(A<string[]>._)).ReturnsLazily(() => currentResult)` — and
set `currentResult` per test (the `testing.md` `ReturnsLazily` pattern). Construct the SUT. One
assertion each:

1. `ParseTheArguments` — after `Execute(args)`,
   `A.CallTo(() => parseVerifyArguments.Parse(args)).MustHaveHappenedOnceExactly()`.
2. `LogTheUsageErrorWhenInvalid` — `currentResult` invalid with a `Message`; assert
   `A.CallTo(() => logger.Error(A<string>._, result.Message)).MustHaveHappenedOnceExactly()`.
3. `NotLogAnErrorWhenValid` — `currentResult` valid; assert
   `A.CallTo(() => logger.Error(A<string>._, A<object>._)).MustNotHaveHappened()`.
4. `CompleteWhenValid` — valid result; `Execute` does not throw (single `NotThrowAsync` assertion).

(Logging assertions are permitted here per `errors-and-logging.md`'s TDD relaxation; the usage-error
log is the observable behavior of a failed invocation, so it is worth pinning.)

**Implement** the `VerifyCommand` rewire to green.

### End-to-end CLI smoke (this item's acceptance, run by hand)

Build once (`dotnet build CodeWorker.sln`), then from the repo root:

1. **Valid invocation** — create two throwaway files and an intent file, then:
   `dotnet run --project CodeWorker.Cli -- verify --intent <existing.json> --production <existing.cs> --tests <existingTests.cs>`
   → console shows the `verify: parsed intent …, production …, tests …` line; process exits 0.
2. **Missing flag** — `dotnet run --project CodeWorker.Cli -- verify --production Foo.cs --tests FooTests.cs`
   → console shows `verify: Missing required flag --intent. Usage: …`; process exits 0 (exit code is
   item 4).
3. **Missing file** — `verify --intent nope.json --production Foo.cs --tests FooTests.cs` (none
   exist) → `verify: Intent file not found: nope.json. Usage: …`.
4. **No arguments** — `dotnet run --project CodeWorker.Cli -- verify` → the `NoArguments` usage line.

Record the observed lines in the Phase Report.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln` — all tests pass
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; capability interface + command in one file;
      no banned patterns; usage failures remain values (nothing thrown); process exit code untouched
- [ ] End-to-end CLI smoke performed for all four cases above; observed output recorded in the report
- [ ] Review loop until all three pass clean, in order, restarting from the top after any fix:
      `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current branch (`CliTester`), message referencing this file; **no push**

Suggested commit message:

```
verify-command-surface phase 3: wire parser into verify command + usage reporting (tasks/todo/foundation/verify-command-surface/03-wire-verify-command.md)
```

## Rollback Procedure

- `git revert <this commit>` restores the Phase 1 `VerifyCommand` skeleton. No dependents, no
  data/config steps. (Phases 1 and 2 can then be reverted independently if desired — revert this
  phase first.)

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); the four end-to-end smoke outputs;
deviation log (an empty log is a claim, not a default); open questions/risks for the reviewer —
restate the ADR-5 flag that a malformed invocation currently logs the usage error but the process
still exits 0 until item 4 (Verdict model + reporting) wires the non-zero exit.

## Hand-off

- **Contract this item now provides to later Foundation items:** a `verify` invocation is parsed
  and validated into a `VerifyArgumentsResult` reachable through the CLI command dispatch. Item 2
  (Intent contract model) consumes `result.IntentPath` to read and validate the intent payload;
  item 3 (Gate pipeline framework) consumes the three paths as pipeline input; item 4 (Verdict model
  + reporting) owns turning a result/verdict into the process exit code and reports.
- **Behavior note:** the valid-result success branch currently only logs a breadcrumb — later items
  replace that branch with the intent parse (item 2) and the gate pipeline run (item 3).
