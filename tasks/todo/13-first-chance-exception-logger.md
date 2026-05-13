# First-Chance Exception Logger

## Objective

When the runner stops mid-task with no log line — see the [22:10:47 silent termination](task.md) where Claude kept working for ~19 more minutes after the runner went silent — one plausible cause is an exception thrown deep in an async path that gets observed by a `catch (Exception)` somewhere up the stack and silently turns into "task ended" instead of "task crashed." The existing [TerminationDiagnostics](CodeWorker/Logging/TerminationDiagnostics.cs) catches **un**handled and **un**observed exceptions, but it cannot see exceptions that *are* handled — and a swallowed-then-eaten exception is invisible.

`AppDomain.CurrentDomain.FirstChanceException` fires the moment an exception is constructed and thrown, **before** any `catch` handler runs. Subscribing to it gives a complete view of every exception flowing through the process, handled or not. With it, an incident like the 22:10:47 stop produces a log entry exactly at the moment the throw happens, naming the type, message, and stack — even if some upstream `catch (Exception)` then quietly swallows it.

The risk is the obvious one: FirstChanceException fires a lot. The implementation must filter aggressively so a healthy run does not bury the log in noise.

## Scope

- [CodeWorker/Logging/TerminationDiagnostics.cs](CodeWorker/Logging/TerminationDiagnostics.cs) — subscribe to `FirstChanceException` from `Install()`. This file already owns "log every signal that explains the runner stopping or misbehaving." Keep it here; do not split into a new class.
- Filter logic: a small private predicate that decides whether to log. Implementation lives in the same file (single consumer).
- New test cases on `TerminationDiagnosticsTests` covering filter behaviour.
- Out of scope:
  - Any production-grade exception aggregation (Application Insights, OpenTelemetry). Logging to Serilog at Debug/Information is the entire scope.
  - Modifying the existing handlers (UnhandledException, UnobservedTaskException, etc.).
  - The power-event work — that is [task 12](tasks/todo/12-power-event-guard.md).

## Design

### Subscription

```csharp
public static void Install()
{
    AppDomain.CurrentDomain.UnhandledException     += OnUnhandledException;
    TaskScheduler.UnobservedTaskException          += OnUnobservedTaskException;
    AppDomain.CurrentDomain.ProcessExit            += OnProcessExit;
    Console.CancelKeyPress                         += OnCancelKeyPress;
    AppDomain.CurrentDomain.FirstChanceException   += OnFirstChanceException;

    RegisterPosixSignal(PosixSignal.SIGTERM);
    // ... existing ...
}
```

### Handler shape

```csharp
private static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs args)
{
    var exception = args.Exception;

    if (!ShouldLog(exception))
    {
        return;
    }

    Log.Debug(
        exception,
        "FirstChanceException Type={ExceptionType} Message={Message}",
        exception.GetType().FullName,
        exception.Message
    );
}
```

Two important properties:

1. **Log level is `Debug`, not `Warning` or `Error`.** A first-chance exception is not necessarily a problem — `int.TryParse` style code paths throw and catch routinely inside the BCL. Default log filters drop Debug, so a healthy production run sees nothing. Operators bump the level to Debug only when investigating.
2. **The handler is reentrancy-safe.** A handler that itself throws inside a FirstChanceException callback re-enters the same callback infinitely. Wrap the body in `try { ... } catch { /* ignored */ }` to prevent that. This is the one place in the codebase where a silent `catch` is correct — per [errors-and-logging.md](.claude/rules/csharp/errors-and-logging.md) the diagnostic-only nature plus the reentrancy risk justifies it; add the required `// reentrancy guard` comment so the intent is explicit.

### Filtering — `ShouldLog`

Without a filter, a normal run is buried. The filter excludes the four categories below by default.

```csharp
private static bool ShouldLog(Exception exception)
{
    return exception switch
    {
        OperationCanceledException => false,
        TaskCanceledException      => false,
        ThreadAbortException       => false,
        _ when IsExpectedBclNoise(exception) => false,
        _ => true,
    };
}

private static bool IsExpectedBclNoise(Exception exception)
{
    var typeName = exception.GetType().FullName ?? string.Empty;

    if (typeName.StartsWith("System.IO.FileNotFoundException", StringComparison.Ordinal) && exception.StackTrace?.Contains("Microsoft.Extensions.FileProviders") == true)
    {
        return true;
    }

    return false;
}
```

Rules captured by the filter:

| Exception                              | Why excluded                                                                            |
|----------------------------------------|-----------------------------------------------------------------------------------------|
| `OperationCanceledException`           | Routine for cooperative cancellation. Not a failure signal.                             |
| `TaskCanceledException`                | Same.                                                                                   |
| `ThreadAbortException`                 | Legacy .NET Framework path; should not appear on .NET 10 but cheap to skip.             |
| `FileNotFoundException` from FileProvider probing | DI frameworks probe assemblies during startup — pure noise.                  |

Anything else falls through and is logged. The discard arm `_ => true` is **not** a switch on enum (the existing rule about throwing `ArgumentOutOfRangeException` in the discard arm only applies to enums); for a pattern-match filter, the discard arm legitimately means "default to logging."

### Why `Debug` level instead of `Information`?

- Many tests in the codebase throw expected exceptions inside FakeItEasy `Throws<T>()` configurations and inside `Assert.Throws`. At `Information` those would appear in every test run, drowning the actual test output.
- Production runs default to `Information` filtering. Operators investigating a silent failure already know to bump to `Debug` for the affected window. Defaulting to `Debug` matches how `LogStreamEvent` in [ClaudeTranscriptTailer.cs:171](CodeWorker/Claude/ClaudeTranscriptTailer.cs#L171) treats routine events.

If the operator hits a silent failure and wants every throw logged from the start of the next run, they bump the Serilog minimum level for the `FatCat.CodeWorker.Logging` namespace to `Debug`. Document this in the task acceptance criteria below.

### Why keep both FirstChance and Unobserved/Unhandled handlers?

They cover disjoint cases:

| Event                       | When it fires                                                                |
|-----------------------------|------------------------------------------------------------------------------|
| `FirstChanceException`      | **Every** throw, before any catch runs.                                      |
| `UnobservedTaskException`   | Only when a Task with a never-observed exception is GC'd.                    |
| `UnhandledException`        | Only when no catch handler exists anywhere up the stack — process is dying.  |

A handled exception in an async method appears only in FirstChance — neither of the other handlers will see it. That is exactly the gap this task fills.

### Performance

The handler runs on the throwing thread. Worst case is a tight loop that throws thousands of exceptions per second (e.g. a parser that uses exceptions for control flow). The filter is two `switch` arms and a string check — single-digit nanoseconds. The Serilog `Log.Debug` call short-circuits when the Debug level is below the global minimum (default Information), so the cost in a healthy run is effectively zero.

If profiling later shows the filter itself is hot, move the type checks to a precomputed `HashSet<Type>` — that is a one-line follow-up, not a design change.

## TDD Plan

Tests live under `CodeWorker.Tests` mirroring source folders. Use `xUnit` + `FakeItEasy` + `FluentAssertions`. Write tests **before** implementation.

### Testing strategy

`AppDomain.CurrentDomain.FirstChanceException` cannot be raised manually from a test — it fires only when the runtime actually constructs an exception. Two options:

1. **Direct-call the handler.** Extract the body of `OnFirstChanceException` into a `LogFirstChanceException(Exception)` static method. Tests call it directly with synthetic exceptions and assert on a captured log sink. The event-subscription line in `Install()` becomes a one-liner forwarder. This is the right shape; mirror what [task 12](tasks/todo/12-power-event-guard.md) does for the power-event handler.
2. **Throw a real exception in a test.** Possible but flaky — relies on the test host having the subscription installed and on log timing. Use option 1.

The actual subscription wiring (option 2's territory) is covered by a single smoke test that asserts `Install()` does not throw and that `AppDomain.CurrentDomain.FirstChanceException` has at least one delegate registered after the call.

### `FirstChanceExceptionLoggerTests` (or whichever name the extracted helper gets)

1. `LogInvalidOperationExceptionAtDebugLevel` — pass `new InvalidOperationException("boom")`; assert one Debug entry containing the type name and message.
2. `LogIncludeStackTraceContext` — pass a thrown-and-caught exception (so `StackTrace` is populated); assert the Debug log call passes the exception itself (so Serilog renders the stack), not just its message.
3. `DoNotLogOperationCanceledException` — assert no log entry.
4. `DoNotLogTaskCanceledException` — assert no log entry.
5. `DoNotLogThreadAbortException` — assert no log entry. (Construct via `FormatterServices.GetUninitializedObject` since the ctor is internal — or skip if the runtime forbids construction; either is acceptable.)
6. `DoNotLogFileNotFoundFromFileProviderProbe` — synthesize an exception whose StackTrace contains `"Microsoft.Extensions.FileProviders"`; assert no log entry.
7. `LogFileNotFoundFromUnrelatedStack` — same exception type, different stack trace; assert one Debug entry.
8. `LogAtMostOneEntryPerCall` — exercise the handler once; assert the sink received exactly one entry.
9. `SwallowExceptionsThrownByTheHandlerItself` — configure the fake logger to throw on its first call; assert the handler returns normally and does not propagate (reentrancy guard).

### `TerminationDiagnosticsTests`

10. `SubscribeToFirstChanceExceptionOnInstall` — call `Install()`; reflect on `AppDomain.CurrentDomain.FirstChanceException` to confirm a non-null delegate. (Reflection on private events is acceptable here — same approach as the power-event smoke test in [task 12](tasks/todo/12-power-event-guard.md). If [task 12](tasks/todo/12-power-event-guard.md) lands first, reuse its helper.)

### Existing tests

- Existing `TerminationDiagnosticsTests` for the other handlers stay green unchanged.

## Implementation Order

**Phase 1 — Extracted helper, tested**
1. Add `FirstChanceExceptionLoggerTests` cases 1–9. Red.
2. Create the extracted `LogFirstChanceException(Exception)` static helper (inside [TerminationDiagnostics.cs](CodeWorker/Logging/TerminationDiagnostics.cs) per the single-consumer rule). Implement the filter and the Debug log call. Green.

**Phase 2 — Wire into TerminationDiagnostics.Install**
3. Add the `AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;` line. Implement `OnFirstChanceException` as a one-liner forwarder around a `try { ... } catch { /* reentrancy guard — see errors-and-logging.md */ }` block.
4. Add `TerminationDiagnosticsTests` case 10. Green.

**Phase 3 — Manual verification**
5. Run the runner with the Serilog minimum level for `FatCat.CodeWorker.Logging` raised to Debug. Confirm:
   - A normal run produces some Debug entries from internal BCL throws (expected — proves the subscription is live).
   - A force-failed task (e.g. delete a required file) produces a Debug entry at the throw site **before** any higher-level handler runs.
6. Lower the minimum back to Information; confirm the noise disappears and `Information`-level handlers (UnhandledException, etc.) still log normally.
7. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`.

## Constraints

- Default log level for the FirstChance handler is **Debug**, never Information or higher. The whole design depends on operators bumping the level only when investigating.
- The handler must be reentrancy-safe. The single allowed `// reentrancy guard` empty-catch is the *only* exception to the no-silent-swallow rule in [errors-and-logging.md](.claude/rules/csharp/errors-and-logging.md).
- Do not log `exception.ToString()` directly — pass the exception object as Serilog's first argument so structured rendering captures it.
- Do not change the existing handlers in [TerminationDiagnostics](CodeWorker/Logging/TerminationDiagnostics.cs).
- The filter list is fixed for this task: `OperationCanceledException`, `TaskCanceledException`, `ThreadAbortException`, and the FileProvider-probe `FileNotFoundException` pattern. Do not add a runtime-configurable allowlist. Configurability can be a follow-up if needed.
- Follow every rule in `.claude/rules/csharp/` — no exceptions. Primary constructors where applicable, block-body methods, file-scoped namespaces, single class per file (these helpers stay inside `TerminationDiagnostics`), no `Async` suffix without overload disambiguation, no `ConfigureAwait(false)`.

## Acceptance Criteria

- With the runner's log level at the default Information, a healthy run produces **no** FirstChanceException entries.
- With the log level raised to Debug for `FatCat.CodeWorker.Logging`, every exception thrown anywhere in the process (except the filtered four) produces a single Debug entry tagged `FirstChanceException` with the exception type, message, and stack.
- A force-failed task that throws inside an async path that is then caught upstack still appears in the FirstChance Debug log even though no other handler fires.
- The handler does not propagate exceptions of its own (reentrancy-safe).
- All existing TerminationDiagnostics handlers (UnhandledException, UnobservedTaskException, ProcessExit, CancelKeyPress, POSIX signals) are unchanged.

Build:
- `dotnet build`, `dotnet test`, `dotnet format` all clean. No new warnings.

## Verification

- [ ] Tests written before implementation (TDD)
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] Must follow all rules `.claude\rules\csharp` no exceptions
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] `dotnet build` to apply CSharpier changes
- [ ] Healthy run at Information level produces zero FirstChance log entries
- [ ] Healthy run at Debug level produces FirstChance entries for genuine throws and zero for the four filtered types
- [ ] Report results before finishing
