# Phase 2 — Exit Codes

- **Work item:** verdict-model-reporting (see `tasks/todo/foundation/verdict-model-reporting/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 6
- **Wave:** 1
- **Parallel with:** Phase 1, Phase 3 — no dependency, disjoint write sets, no shared hotspot
- **Risk:** **Medium** — an unexpected crash now exits 3 instead of 0, which every caller of the CLI sees.

## Touches

- Creates: `CodeWorker.Cli/CliExitCode.cs`, `CodeWorker.Cli/Process/SetExitCode.cs`
- Modifies: `CodeWorker.Cli/Program.cs`
- Exclusive runtime resources: none

Other phases run at the same time in their own worktrees. Create or modify **only** these files.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md` ("Usage" — exit 0 green, non-zero on a block), `CLAUDE.md`, and
**all** `.claude/rules/csharp/*.md` first — mandatory. Especially `testing.md` ("Low-Level API
Implementations — No Unit Tests Required") and `errors-and-logging.md` (log-and-handle at the
top-level boundary only).

**Worktree base:** before reading code, run `git merge --ff-only <wave base>` (the commit the
orchestrator names) in your still-clean worktree, then confirm `git rev-parse HEAD` equals it.

Current state: no code in the CLI sets an exit code. `CodeWorker.Cli/Program.cs`:
```csharp
public static async Task Main(params string[] args)
{
	ConsoleLog.LogCallerInformation = true;
	try { SystemScope.Initialize(...); var application = SystemScope.Container.Resolve<CodeWorkerCliApplication>(); await application.Run(args); }
	catch (Exception ex) { ConsoleLog.WriteException(ex); }
	finally { Log.CloseAndFlush(); Console.Out.Flush(); }
}
```
The main project already has a `Process/` folder (`CodeWorker/Process/`, namespace
`FatCat.CodeWorker.Process`) for process-level abstractions — mirror it in the CLI.

## Design (build exactly this shape)

`CodeWorker.Cli/CliExitCode.cs`, namespace `FatCat.CodeWorker.Cli`:
```csharp
public enum CliExitCode
{
	Success = 0,
	Blocked = 1,
	InvalidInput = 2,
	UnexpectedError = 3,
}
```

`CodeWorker.Cli/Process/SetExitCode.cs`, namespace `FatCat.CodeWorker.Cli.Process` — interface and
class in one file:
```csharp
public interface ISetExitCode
{
	void Set(CliExitCode exitCode);
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over Environment.ExitCode — no business logic, tested via ISetExitCode fakes in consuming classes."
)]
public class SetExitCode : ISetExitCode
{
	public void Set(CliExitCode exitCode)
	{
		Environment.ExitCode = (int)exitCode;
	}
}
```

`Program.Main` catch block: after `ConsoleLog.WriteException(ex);` add
`Environment.ExitCode = (int)CliExitCode.UnexpectedError;`. Nothing else in `Program` changes.

`ISetExitCode` → `SetExitCode` resolves by `SystemScope` scanning; no module change.

## Steps

There is no business logic to test-drive here: `SetExitCode` is an excluded low-level wrapper and
`Program` is the bootstrap boundary (not unit-tested in this codebase). Behaviour is proven by
Phase 6's unit tests (faked `ISetExitCode`) and E2E exit-code assertions.

1. Add the three changes above.
2. `dotnet build` / `dotnet test` — the existing suite (including E2E, which still expects exit 0 on
   the paths it covers — none of them throw) stays green.
3. Manual smoke (record in the Phase Report): confirm a normal `verify` run still exits 0
   (`dotnet run --project CodeWorker.Cli -- verify …; echo $?`).

## Definition of Done (all mandatory)

- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass (existing E2E unchanged)
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; specific `[ExcludeFromCodeCoverage]` justification; no banned patterns
- [ ] Only the files in `## Touches` created/modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS` — the excluded wrapper and the bootstrap need no unit tests) → `code-review` → `code-security-review` (screen-only — write nothing outside `## Touches`)
- [ ] Exactly one commit on your worktree branch, message referencing this file; no push

Suggested commit message:

```
verdict-model-reporting phase 2: exit codes (tasks/todo/foundation/verdict-model-reporting/02-exit-codes.md)
```

## Rollback Procedure

- Revert Phase 6 first if committed, then `git revert <this commit>`. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts; smoke output; deviation log (an empty log is a claim, not a
default); open questions/risks.

## Hand-off

- `CliExitCode` (namespace `FatCat.CodeWorker.Cli`) and `ISetExitCode.Set(CliExitCode)`
  (namespace `FatCat.CodeWorker.Cli.Process`) — Phase 6 injects `ISetExitCode` into `DeliverVerdict`
  and `VerifyCommand`.
- An exception reaching `Program.Main` now exits 3.
