# What NOT to Do

These are hard stops. Do not do any of the following under any circumstances.

## Type System
- Do NOT add nullable reference-type annotations (`string?`, `ILogger?`) — nullable is disabled in this project (`<Nullable>disable</Nullable>`). The `?` annotation is only meaningful on value types, and only where the value is genuinely optional.
- Do NOT annotate defensively or add null checks to values that are always populated.
- Do NOT use records — use classes only

## Async
- Do NOT use `async void` — always return `Task` or `Task<T>`
- Do NOT use `ConfigureAwait(false)` — we do not use it
- Do NOT block on tasks with `.Result` or `.Wait()`
- Do NOT use `Task` or `Thread` directly for threading — use `IThread`. No `Task.Delay`, no `Thread.Sleep`, no `new Thread(...)`.
- Do NOT call `DateTime.UtcNow` or `DateTime.Now` in business logic where the value drives testable behaviour — inject `IClock` and read `clock.UtcNow`. (Low-level `[ExcludeFromCodeCoverage]` plumbing is the only exception.)

## Code Style
- Do NOT use expression-bodied members (`=>` syntax for methods or properties) — this applies to ALL access levels (public, private, protected, internal) and ALL projects including test projects
- Do NOT use query syntax LINQ (`from x in y where...`) — method chaining only
- Do NOT use string concatenation with `+` — use string interpolation. Write `$"Some string with data {theData}"`, never `"Some string with data " + theData`. (No analyzer enforces this; it is caught by code review.)
- Do NOT abbreviate names — write them out fully
- Do NOT write comments explaining what code does — rename until obvious
- Do NOT use `new List<T>()`, `new T[0]`, or `new Dictionary<K, V>()` for empty or inline-populated collections — use collection expressions (`[]`)

## Architecture
- Do NOT use property injection or setter injection — constructor only
- Do NOT use `new` inside a class to instantiate a dependency
- Do NOT name a file after an interface — always name after the class
- Do NOT add abstractions or patterns that do not exist in the surrounding codebase
- Do NOT introduce over-engineering — match the abstraction level of the existing code
- Do NOT use `SystemScope` / `ISystemScope` as a service locator to avoid constructor injection — only use it for the root resolution in `Program.Main` or genuine runtime resolution

## Errors & Logging
- Do NOT throw exceptions for predictable, known failure states — return an enum
- Do NOT swallow exceptions silently
- Do NOT inject `Microsoft.Extensions.Logging.ILogger` — always inject `Serilog.ILogger`
- Do NOT use `ConsoleLog` as a scratch debugger — if you add a temporary trace, remove it before merging. It is acceptable in permanent code only for genuine user-facing console output or boot-time announcements.

## Testing
- Do NOT use `A<T>.Ignored` in FakeItEasy argument matchers — always use `A<T>._`

## Formatting
- Do NOT manually fight CSharpier formatting — it is the final authority
- Do NOT suppress `dotnet format` / analyzer warnings without a comment explaining why
