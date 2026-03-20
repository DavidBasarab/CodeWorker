# Not Allowed

These are banned outright. Do not use them. Do not suggest them.

## C# Banned Patterns
- Nullable types (`?`) — banned entirely
- `record` types — use classes only
- `struct` — use classes only
- Query syntax LINQ (`from x in y select z`) — use method chaining only
- Expression-bodied methods or properties (`=>`) — use block bodies only
- `async void` — always return `Task` or `Task<T>`
- `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` — never block on a Task
- `new` to instantiate dependencies — use constructor injection
- `ConfigureAwait(false)` — not used in this codebase
- `Task` or `Thread` directly for threading — use `IThread`
- String concatenation with `+` — use string interpolation
- Spelling out types explicitly when `var` works

## Structural Bans
- No comments explaining what code does — rename instead
- No abbreviations that don't meet the top-3 Google rule
- No more than one class per file (except interface + direct implementation)
- No namespace that does not match the folder path exactly
- No dead code — if code is no longer needed, delete it entirely
- No classes made redundant by abstraction — when a generic or base class replaces a concrete class, delete the concrete class and remove all references to it
