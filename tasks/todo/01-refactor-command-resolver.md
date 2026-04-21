# Refactor CommandResolver to use a switch expression

## Goal
Replace the if/else chain in [CodeWorker/Commands/CommandResolver.cs](CodeWorker/Commands/CommandResolver.cs) with a switch expression. The current code has five sequential `if (string.Equals(commandName, "...", StringComparison.OrdinalIgnoreCase))` blocks — the project's C# rules explicitly ban this pattern in favor of switch expressions with a discard arm.

## Scope
- Only `CodeWorker/Commands/CommandResolver.cs` changes.
- The existing tests in [CodeWorker.Tests/Commands/CommandResolverTests.cs](CodeWorker.Tests/Commands/CommandResolverTests.cs) must continue to pass without modification. They verify the resolver behavior — the refactor must be behavior-preserving.

## Target shape
```csharp
public ICommand Resolve(string[] args)
{
    if (args.Length == 0) return runTaskCommand;

    return args[0].ToLowerInvariant() switch
    {
        "setup"   => setupCommand,
        "track"   => trackCommand,
        "untrack" => untrackCommand,
        "list"    => listCommand,
        "info"    => infoCommand,
        _         => runTaskCommand,
    };
}
```

Note: the default arm `_ => runTaskCommand` is the intentional fallback (current behavior when the command name is unrecognized — it falls through to `runTaskCommand`). Do NOT throw `ArgumentOutOfRangeException` here; this is not an enum switch, and unknown input is explicitly handled.

## Definition of done
- `Resolve` is a single method using a switch expression.
- All existing `CommandResolverTests` pass.
- `dotnet build` is clean; CSharpier is satisfied.
