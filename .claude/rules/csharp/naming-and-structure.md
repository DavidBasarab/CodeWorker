# Naming & Structure

## Core Philosophy
- Follow Clean Code principles (Robert C. Martin) and SOLID.
- Methods do one thing. Classes have one responsibility.
- Code reads like prose. Names make intent obvious without reading the implementation.
- Prefer interfaces and polymorphism over if/switch chains.
- Do NOT over-engineer. Do NOT introduce abstractions that do not already exist in this codebase.
- Match the abstraction level and style of the surrounding code.

## Naming Rules
- Avoid abbreviations. Prefer full words so readers never have to guess meaning.
- Acceptable abbreviations: widely recognized acronyms (e.g. `HTTP`, `URL`, `ID`, `CLI`) and any abbreviation that appears among the top 3 Google results for that term. When in doubt, use the full word.
- Names reveal intent. A method name makes it unnecessary to read the body.
- No comments explaining what code does — rename until it is obvious.
- PascalCase: classes, interfaces, methods, properties, constants
- camelCase: local variables, parameters, private fields — no leading underscore
- Private fields prefer `readonly` for dependencies where applicable
- Boolean names read as questions or states: `isReady`, `hasOutputs`, `canRestore`
- String interpolation required — never string concatenation with `+`
- Do NOT suffix method names with `Async` just because they return a `Task`. Name the method after what it does: `Save`, not `SaveAsync`. Only use the `Async` suffix when a non-async overload with the same name already exists and both must coexist.

## Discards
- Use `_` to discard outputs you intentionally do not need — `out _` for ignored out parameters, `using var _ = ...` for disposables acquired only for their side effect.

## Method Size
- Methods should be as short as possible.
- ~10 lines is a signal to evaluate refactoring — not an automatic rule.
- No method should require a comment to explain what it does. Refactor or rename instead.

## Spacing
- Leave a blank line between method definitions.
- Leave a blank line after variable declarations in a method before logic begins.
- Leave a blank line before return statements.

## Control Flow
- Avoid deep if/else nesting. Prefer guard clauses and early returns to keep the main flow readable.
- Avoid complex nested ternary expressions — prefer clear `if` statements or extract into a well-named method.
- If you need to explain what code does with a comment, first ask whether a better name makes the comment unnecessary.
- Use switch expressions (not if/else chains) when branching on an enum or type. Always include a discard arm `_` that throws `ArgumentOutOfRangeException` for unhandled cases:

```csharp
// Correct — switch expression
var handler = taskOutcome switch
{
    TaskOutcome.Done => doneHandler,
    TaskOutcome.Blocked => blockedHandler,
    TaskOutcome.Failed => failedHandler,
    _ => throw new ArgumentOutOfRangeException(nameof(taskOutcome)),
};

// Wrong — if/else chain
if (taskOutcome == TaskOutcome.Done) handler = doneHandler;
else if (taskOutcome == TaskOutcome.Blocked) handler = blockedHandler;
```

The `CommandResolver` dispatch on `args[0]` is the canonical example of this in the codebase — a switch expression mapping each verb to a command, with a default arm.

## Files & Namespaces
- One class per file. File named after the class, never the interface.
- When a class directly implements a single interface, the interface and class live in the same file — named after the class. Do not create a separate file for the interface.
- Only create a standalone interface file when the interface has multiple implementations or is consumed without a single obvious implementation.
- Namespace must exactly match the folder path within the project. No exceptions.
- All production namespaces start with `FatCat.CodeWorker.*` (e.g. `FatCat.CodeWorker.Commands`, `FatCat.CodeWorker.Commands.Run`, `FatCat.CodeWorker.History`, `FatCat.CodeWorker.Logging`).
- Test project mirrors source project: same folder structure, same namespace with the `Testing.` prefix — `FatCat.CodeWorker.Commands.Info` → `Testing.FatCat.CodeWorker.Commands.Info`.
- Always use file-scoped namespaces (C# 10+). Never use block-style `namespace X { }`.

```csharp
// Correct — file-scoped
namespace FatCat.CodeWorker.Commands.Info;

public class InfoCommand { }

// Wrong — block-scoped
namespace FatCat.CodeWorker.Commands.Info
{
    public class InfoCommand { }
}
```

## Command Pattern

CodeWorker is a console application. Every user-invokable action is an `ICommand`, and `args[0]` selects which one runs.

```csharp
public interface ICommand
{
    Task Execute(string[] args);
}
```

1. **One command per verb, one file per command.** A command class handles a single CLI verb (`setup`, `track`, `list`, `info`, `run-task`, `help`, …) and lives in its own folder under `Commands/` named after the feature. The file is named after the class.

2. **Capability interface extends `ICommand`.** Each command exposes a narrow marker interface that extends `ICommand`, so the container can inject and the resolver can dispatch by role rather than by concrete type. Define it in the same file, immediately above the class:

```csharp
namespace FatCat.CodeWorker.Commands.Info;

public interface IRunInfoCommand : ICommand { }

public class InfoCommand(ILoadRunHistory loadRunHistory, ILogger logger) : IRunInfoCommand
{
    public async Task Execute(string[] args)
    {
        ...
    }
}
```

3. **Resolution is a switch expression.** New verbs are wired into `CommandResolver.Resolve` — a switch expression over `args[0].ToLowerInvariant()` with a default arm. Inject the new command's capability interface into `CommandResolver`; do not new it up.

```csharp
return args[0].ToLowerInvariant() switch
{
    "setup" => setupCommand,
    "info" => infoCommand,
    ...
    _ => runTaskCommand,
};
```

4. **Positional arguments.** Commands read their own arguments from the `args` array (e.g. `args.Length > 1 ? args[1] : default`). Keep argument parsing inside the command; do not spread it across helpers unless it earns its own well-named method.

5. **Mutable state fields for working context.** When a command or a processing class breaks its logic into multiple private helper methods, it may use non-`readonly` private fields to share working state across those methods within a single invocation. These fields are intentionally mutable and are not injected — they are populated during execution. Nullable reference types are disabled in this project, so declare them plainly (no `null!`):

```csharp
public class ProcessTask(IDiscoverTasks discoverTasks, IClassifyTaskResult classifyTaskResult, ILogger logger)
{
    private TaskExecutionContext context;   // working state — intentionally NOT readonly

    public async Task Process(string repositoryPath)
    {
        context = await BuildContext(repositoryPath);

        await RunAndClassify();
    }

    private async Task RunAndClassify() { ... }
}
```

This pattern avoids passing many parameters between helper methods. It is only valid within a class whose lifetime is a single unit of work.

## Type-Role Suffix Conventions
The codebase uses a consistent vocabulary of type-role suffixes. Pick the existing suffix for the role — do not invent new ones.

| Suffix | Role |
|---|---|
| `*Command` | A CLI command (`ICommand` implementation) — one per verb |
| `*Entry` | A persisted record (JSONL run-history line), e.g. `RunHistoryEntry`, `RepositoryRunHistoryEntry` |
| `*Result` | An outcome value returned from an operation, e.g. `RepositoryValidationResult` |
| `*Context` | Working state passed through a single unit of work, e.g. `TaskExecutionContext` |
| `*Heuristic` | A rule that classifies a task result, e.g. `TokenLimitHeuristic`, `TimedOutHeuristic` |
| `*Handler` | An `ITaskOutcomeHandler` for a `TaskOutcome`, e.g. `HandleDoneTaskOutcome` |
| `*Factory` | A type that constructs another based on runtime input, e.g. `TaskOutcomeHandlerFactory` |
| `*Module` | The Autofac `Module` for a project |

Match the existing layout. Do not place a new command, heuristic, or handler in an arbitrary location.

## Folder Conventions
- `Commands/` — one sub-folder per feature/verb (`Setup/`, `Run/`, `Track/`, `Info/`, …), each holding that command and the helpers it owns.
- `Commands/Run/Heuristics/` — task-result classification rules.
- `Commands/Run/Outcomes/` — `ITaskOutcomeHandler` implementations and their factory.
- `History/` — run-history persistence types.
- `FileSystem/`, `Git/`, `Process/` — thin abstractions over the OS / external processes (see Interfaces below).
- `Logging/`, `Settings/` — Serilog configuration and app/repository settings.

## Interfaces
- All interfaces use the `I` prefix.
- Interface names describe a capability or action: `IResolveCommand`, `ISetupRepository`, `ILoadRunHistory`, `IClassifyTaskResult`, `IDiscoverTasks`, `IRunGitWorkflow`.
- NOT: `ICommandResolver`, `IRepository`, `IHistory` — these describe what something is, not what it does. (The `*Command` marker interfaces like `IRunInfoCommand` are the deliberate exception: they name the command's role and extend `ICommand`.)
- Default to narrow, single-purpose interfaces. One interface = one capability.
- All cross-boundary dependencies must be interfaces: the file system, git, external process execution (the Claude CLI), threading, time. This is why `IFileSystemTools`, `IAppendFile`, `IGetWorkingDirectory`, and the git/process abstractions exist.
- If something cannot be faked in a test, it is not properly abstracted.
