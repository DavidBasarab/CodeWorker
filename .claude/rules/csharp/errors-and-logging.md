# Error Handling & Logging

## Error Handling
- Exceptions are for unplanned, unexpected failures only (hardware failures, network timeouts, corrupted state).
- Never throw an exception for a predictable outcome (validation failure, value out of range, known bad state).
- For known failure modes, return a value — an enum is preferred.
- Let exceptions bubble to the boundary where they can be meaningfully handled.
- Do not catch and swallow exceptions silently. The one exception: if a failure is genuinely non-actionable (e.g. a socket error on disconnect, a reflection comparison on an incompatible type), an empty catch with a `// ignored` comment is acceptable. This must be rare and deliberate — never use it to hide logic errors.

```csharp
// Preferred for known failures:
public enum ResetResult { Success, BackupNotFound, DeviceBusy }

public ResetResult TryReset()
{
    if (!backupExists) return ResetResult.BackupNotFound;
    if (deviceIsBusy)  return ResetResult.DeviceBusy;
    ExecuteReset();
    return ResetResult.Success;
}
```

## Logging — Serilog
- We use Serilog.
- Permanent logging: inject `ILogger` via the constructor.
- `DevLog` is a static logger for development debugging only — remove before merging. Do not generate `DevLog` calls in permanent code.
- Log at the action site, not at the boundary.
- Log thoughtfully — do not add log entries without a clear reason.
- Active log levels: `Debug`, `Information`, `Warning`, `Error`.

## Logging and TDD
- Logging is the one area where strict TDD is not enforced.
- Do not block on log string test coverage — test critical entries, use judgment for the rest.
