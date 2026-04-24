# Simplify InfoCommand history slicing

## Goal
Small cleanup in [CodeWorker/Commands/Info/InfoCommand.cs](CodeWorker/Commands/Info/InfoCommand.cs). The current line:

```csharp
var entries = history.Skip(Math.Max(0, history.Count - count)).ToList();
```

expresses "take the last N entries" indirectly — the intent isn't obvious without reading the math. Replace with:

```csharp
var entries = history.TakeLast(count).ToList();
```

`TakeLast` handles the case where `count` exceeds `history.Count` without the manual `Math.Max` guard, and the name states intent directly.

## Scope
- Single-line change inside `InfoCommand.Execute`.
- Verify existing `InfoCommandTests` still pass — `TakeLast` and the `Skip(Math.Max(...))` expression produce the same output for all valid inputs.

## Definition of done
- One line changed.
- All tests green; CSharpier clean.
