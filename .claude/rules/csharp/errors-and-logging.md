# Error Handling & Logging

## Error Handling
- Exceptions are for unplanned, unexpected failures only (hardware failures, process crashes, corrupted state).
- Never throw an exception for a predictable outcome (validation failure, value out of range, known bad state).
- For known failure modes, return a value — an enum is preferred.
- Let exceptions bubble to the boundary where they can be meaningfully handled.
- Do not catch and swallow exceptions silently. The one exception: if a failure is genuinely non-actionable (e.g. a reflection comparison on an incompatible type, cleanup of a file that may already be gone), an empty catch with a `// ignored` comment is acceptable. This must be rare and deliberate — never use it to hide logic errors.
- "Log and rethrow at the boundary" is allowed at the top-level entry point only (e.g. `Program.Main` catching `Exception` and calling `ConsoleLog.WriteException(ex)`). Do not log-and-rethrow at every layer — pick one boundary.

```csharp
// Preferred for known failures:
public enum SetupResult { Success, RepositoryNotFound, AlreadyConfigured }

public SetupResult TrySetup(string repositoryPath)
{
    if (!repositoryExists)   return SetupResult.RepositoryNotFound;
    if (alreadyConfigured)   return SetupResult.AlreadyConfigured;
    ConfigureRepository();
    return SetupResult.Success;
}
```

## Logging — Serilog
- We use Serilog. Inject `Serilog.ILogger` via the constructor for permanent logging (`using Serilog;` then take `ILogger logger` in the primary constructor). Never inject `Microsoft.Extensions.Logging.ILogger`.
- Log at the action site, not at the boundary.
- Log thoughtfully — do not add log entries without a clear reason.
- Active log levels: `Debug`, `Information`, `Warning`, `Error`.

### ConsoleLog
`ConsoleLog` (`FatCat.Toolkit.Console`) provides colour-coded console writes (`WriteGreen`, `WriteYellow`, `WriteRed`, `WriteException`, etc.). It backs the Serilog console sink and is used at the top-level entry point (`Program.Main` calls `ConsoleLog.WriteException` for a fatal boot-time failure). Acceptable in permanent code where user-facing console output or a one-off boot-time announcement is genuinely useful.

`ConsoleLog` is not a scratch debugger. If you add a temporary trace while diagnosing, remove it before merging. In normal business logic prefer the injected `ILogger` when you have an instance.

## Logging and TDD
- Logging is the one area where strict TDD is not enforced.
- Do not block on log string test coverage — test critical entries, use judgment for the rest.
