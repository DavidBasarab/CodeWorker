# Types & Dependency Injection

## Type Declarations
- Use `var` everywhere the type is inferable. Never spell out the type explicitly when `var` works.
- Nullable types (`?`) are banned. Do not enable nullable in the project.
- `record` types are banned. Use classes only.
- Default to `public`. Do not add access modifiers unless restricting access is intentional.

## Classes
- Classes only. No structs, no records.
- Primary constructors (C# 12+) for all service classes.

## Dependency Injection
- Constructor injection only — never use `new` to instantiate a dependency.
- All dependencies are injected as interfaces.
- Never use a service locator or resolve dependencies manually.

```csharp
public class MyService(IDoSomething doSomething,
                       IThread thread,
                       ILogger logger)
{
    public void Execute()
    {
        // use injected dependencies
    }
}
```

## IThread — Threading Abstraction
- Threading and sleep operations use `IThread`. Never use `Task` or `Thread` directly.
- `IThread` is injected via constructor like all other dependencies.
- `FakeThread` provides a synchronous substitute for unit tests.

## LINQ
- Method chaining syntax only. Query syntax (`from x in y select`) is banned.
- CSharpier handles formatting of chained calls.
