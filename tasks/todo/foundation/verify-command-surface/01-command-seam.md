# Phase 1 — Command-Pattern Seam + `verify` Command Skeleton

- **Work item:** verify-command-surface (see
  `tasks/todo/foundation/verify-command-surface/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 3 (`03-wire-verify-command.md`)
- **Risk:** **Medium** — rewires the single dispatch path every CLI invocation flows through and
  replaces the `ProcessArguments` stub behavior (and its tests). No auth, no anonymous endpoint, no
  data migration, no public/network API contract — so not auto-high, but flagged for review because
  it changes the app's entry-path behavior in one commit.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Pay special attention to `naming-and-structure.md` (command pattern, capability
interface, switch-expression dispatch, one class per file, interface + single impl in the same
file) and `testing.md` (verb-first one-assertion tests, FakeItEasy, block bodies).

**This phase introduces the CLI's first command infrastructure.** Mirror the main `CodeWorker`
project — do not invent a CLI-specific shape.

Current state you will find:

- `CodeWorker.Cli/Commands/ProcessArguments.cs` — `IProcessArguments` + `ProcessArguments(ILogger)`
  stub: empty-args logs `"No arguments provided"`, else logs the argument count, then
  `await Task.CompletedTask`. **You will replace the body** with resolve-and-execute dispatch.
- `CodeWorker.Cli/CodeWorkerCliApplication.cs` — `Run` logs the welcome line then calls
  `processArguments.Process(args)`. **Leave it unchanged.**
- `CodeWorker.Cli.Tests/Commands/ProcessArgumentsTests.cs` — asserts the stub's logging. **You will
  rewrite it** to assert the new dispatch behavior (the old log-count behavior is deliberately gone).
- No `ICommand` / `CommandResolver` exists in the CLI yet.

The exact pattern to copy (main project):
- `CodeWorker/Commands/ICommand.cs` — `public interface ICommand { Task Execute(string[] args); }`
- `CodeWorker/Commands/CommandResolver.cs` — `IResolveCommand { ICommand Resolve(string[] args); }`
  + a class with a `switch` on `args[0].ToLowerInvariant()` and a default arm.
- `CodeWorker/Commands/Info/InfoCommand.cs` — `public interface IRunInfoCommand : ICommand { }`
  declared immediately above `public class InfoCommand(...) : IRunInfoCommand`, same file.

Registration: nothing goes in `CodeWorkerCliModule`. `IResolveCommand`, `IRunVerifyCommand`, and
`IProcessArguments` each have one implementation and auto-register via `SystemScope` scanning
(overview ADR-4).

## Design (build exactly this shape)

**`CodeWorker.Cli/Commands/ICommand.cs`** — namespace `FatCat.CodeWorker.Cli.Commands`:

```csharp
namespace FatCat.CodeWorker.Cli.Commands;

public interface ICommand
{
	Task Execute(string[] args);
}
```

**`CodeWorker.Cli/Commands/Verify/VerifyCommand.cs`** — namespace
`FatCat.CodeWorker.Cli.Commands.Verify`. A **skeleton** this phase: it exists so the resolver has a
command to return and the dispatch works end to end. Phase 3 gives it the parser and usage
reporting. For now it logs at Debug and completes:

```csharp
using FatCat.CodeWorker.Cli.Commands;
using Serilog;

namespace FatCat.CodeWorker.Cli.Commands.Verify;

public interface IRunVerifyCommand : ICommand { }

public class VerifyCommand(ILogger logger) : IRunVerifyCommand
{
	public Task Execute(string[] args)
	{
		logger.Debug("Verify command invoked");

		return Task.CompletedTask;
	}
}
```

Note: block body returning `Task.CompletedTask` (no `async` keyword) — there is no awaitable work
yet, and `async` with no `await` trips CS1998 under the zero-warning gate.

**`CodeWorker.Cli/Commands/CommandResolver.cs`** — namespace `FatCat.CodeWorker.Cli.Commands`:

```csharp
using FatCat.CodeWorker.Cli.Commands.Verify;

namespace FatCat.CodeWorker.Cli.Commands;

public interface IResolveCommand
{
	ICommand Resolve(string[] args);
}

public class CommandResolver(IRunVerifyCommand verifyCommand) : IResolveCommand
{
	public ICommand Resolve(string[] args)
	{
		if (args.Length == 0)
		{
			return verifyCommand;
		}

		return args[0].ToLowerInvariant() switch
		{
			"verify" => verifyCommand,
			_ => verifyCommand,
		};
	}
}
```

The named `"verify"` arm and the `_` default both return the verify command **on purpose** — that
duplicated arm is the extension seam where the second command drops in (overview ADR-2). Call this
out in the Phase Report so `code-review` does not "simplify" the switch away.

**`CodeWorker.Cli/Commands/ProcessArguments.cs`** — rewire `Process` to dispatch. Keep the
`IProcessArguments` interface; swap the dependency from `ILogger` to `IResolveCommand`:

```csharp
namespace FatCat.CodeWorker.Cli.Commands;

public interface IProcessArguments
{
	Task Process(string[] args);
}

public class ProcessArguments(IResolveCommand resolveCommand) : IProcessArguments
{
	public async Task Process(string[] args)
	{
		var command = resolveCommand.Resolve(args);

		await command.Execute(args);
	}
}
```

## Steps (TDD — tests first, red before green)

1. **`CommandResolverTests`** (`CodeWorker.Cli.Tests/Commands/CommandResolverTests.cs`, namespace
   `Testing.FatCat.CodeWorker.Cli.Commands`). Fake `IRunVerifyCommand`; construct
   `CommandResolver`. One assertion each:
   - `ResolveTheVerifyCommandForTheVerifyVerb` — `Resolve(["verify"])` `.Should().Be(verifyCommand)`.
   - `ResolveTheVerifyCommandCaseInsensitively` — `Resolve(["VERIFY"])` returns `verifyCommand`.
   - `ResolveTheVerifyCommandForAnUnknownVerb` — `Resolve(["nonsense"])` returns `verifyCommand`
     (documents ADR-2).
   - `ResolveTheVerifyCommandWhenNoArgumentsProvided` — `Resolve([])` returns `verifyCommand`.

2. **`ProcessArgumentsTests`** — rewrite the existing file. Fake `IResolveCommand` and a returned
   `ICommand`; in the constructor
   `A.CallTo(() => resolveCommand.Resolve(A<string[]>._)).Returns(resolvedCommand)`. One assertion
   each:
   - `ResolveTheCommandFromArgs` — after `Process(args)`,
     `A.CallTo(() => resolveCommand.Resolve(args)).MustHaveHappenedOnceExactly()`.
   - `ExecuteTheResolvedCommand` — `A.CallTo(() => resolvedCommand.Execute(args)).MustHaveHappenedOnceExactly()`.

3. **`VerifyCommandTests`** (`CodeWorker.Cli.Tests/Commands/Verify/VerifyCommandTests.cs`, namespace
   `Testing.FatCat.CodeWorker.Cli.Commands.Verify`). Fake `ILogger`; construct `VerifyCommand`.
   - `CompleteWithoutError` — `await command.Execute(Faker.Create<string[]>())` does not throw
     (a single behavioral assertion for the skeleton; Phase 3 adds the real behavior). Use
     `await FluentActions.Awaiting(() => command.Execute(args)).Should().NotThrowAsync();` or an
     equivalent single-assertion form.

4. **Implement** `ICommand`, `VerifyCommand` (+ `IRunVerifyCommand`), `CommandResolver`
   (+ `IResolveCommand`), and the `ProcessArguments` rewire to green.

5. **Smoke-check** manually: `dotnet run --project CodeWorker.Cli -- verify` — the app prints the
   welcome line and exits cleanly (the Debug line may be below the console sink's default level;
   that is fine). `dotnet run --project CodeWorker.Cli` with no args also exits cleanly.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln` — all tests pass
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespaces match folder paths exactly; one class per file; capability interface
      (`IRunVerifyCommand`) and its command in the same file; `*Command`/`*Resolver` suffixes
      correct; no banned patterns (no expression-bodied members, no `async void`, no records, no
      `new List<T>()`, collection expressions where applicable)
- [ ] Review loop until all three pass clean, in order, restarting from the top after any fix:
      `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current branch (`CliTester`), message referencing this file; **no push**

Suggested commit message:

```
verify-command-surface phase 1: CLI command-pattern seam + verify skeleton (tasks/todo/foundation/verify-command-surface/01-command-seam.md)
```

## Rollback Procedure

- If Phase 3 exists, revert it first (it depends on this phase). Then `git revert <this commit>`.
- No data/config/feature-flag steps. Reverting restores the `ProcessArguments` stub and its tests.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts (new/total/passing); deviation log (every departure from
this plan and why — an empty log is a claim, not a default). **Explicitly note** that
`CommandResolver`'s duplicated `"verify"`/`_` arms are the intentional extension seam (ADR-2), so a
later reviewer does not collapse the switch. Open questions/risks for the reviewer.

## Hand-off

- **Interfaces/types this phase exposes to later phases:**
  - `FatCat.CodeWorker.Cli.Commands.ICommand` — `Task Execute(string[] args)`.
  - `FatCat.CodeWorker.Cli.Commands.IResolveCommand` / `CommandResolver` — the dispatch seam;
    resolves `args[0]` to a command (currently always the verify command).
  - `FatCat.CodeWorker.Cli.Commands.Verify.IRunVerifyCommand` / `VerifyCommand` — the verify
    command; **skeleton** (`Execute` logs Debug and completes). Phase 3 replaces the body.
  - `ProcessArguments` now depends on `IResolveCommand` and dispatches; it no longer logs arg counts.
- **Behavior notes for later phases:** the second CLI command is added by giving the resolver its
  capability interface and a named `switch` arm — the default arm stays as the fallback. `verify`
  reaches `VerifyCommand.Execute` with the **full** args array (including `args[0] == "verify"`);
  the Phase 2 parser must account for the leading verb.
