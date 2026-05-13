# Power-Event Guard — Log System Suspend/Resume

## Objective

When the runner stops mid-task with no log line — exactly what happened in the [recent silent termination at 22:10:47](task.md) where Claude kept working for ~19 more minutes after the runner went silent — the most common cause is the machine going to sleep. Today the runner has no visibility into that event. The first time anyone notices is "why did it just stop?" with nothing in the log to point at.

Subscribe to Windows power events so a Suspend / Resume transition produces a clear log line **before** anything else fails. After this task, the log of a sleep-killed run reads:

```
22:10:47 [INF] Tailer heartbeat ... events=66 ...
22:10:52 [WRN] Power mode change Suspend
```

instead of just silence. Resume produces a matching entry on wake.

The runner does not try to *recover* from a Suspend — that is what [RecoverPendingTasks](CodeWorker/Claude/RecoverPendingTasks.cs) is for on the next launch. The goal of this task is purely diagnostic: leave a breadcrumb so post-mortem analysis is one-second instead of guess-the-cause.

## Scope

- [CodeWorker/Logging/TerminationDiagnostics.cs](CodeWorker/Logging/TerminationDiagnostics.cs) — add subscription to power events alongside the existing UnhandledException / UnobservedTaskException / ProcessExit / CancelKeyPress / POSIX-signal handlers. This class already owns "log every signal that could explain the runner stopping," so power events belong here. Do not create a new class.
- [CodeWorker/CodeWorker.csproj](CodeWorker/CodeWorker.csproj) — add `Microsoft.Win32.SystemEvents` NuGet reference (the package containing `SystemEvents.PowerModeChanged`). It is a Windows-only API.
- New test class `TerminationDiagnosticsTests` if one does not already exist, or extend it if it does. Power-event tests can only verify the wiring (delegate registration) without actually putting the machine to sleep — see TDD Plan for the seam.
- Out of scope:
  - Any attempt to *suspend* the wrapper or stop running tasks before sleep. That is its own design problem (cancellation tokens propagating through `IThread.Sleep`, the wrapper being detached from the runner process, etc.) and not needed to fix the diagnostic gap.
  - Cross-platform power events. On Linux/macOS the equivalent (`org.freedesktop.login1` / `IORegistry`) is a different API surface. Out of scope — gate by `OperatingSystem.IsWindows()`.

## Design

### `SystemEvents.PowerModeChanged`

`Microsoft.Win32.SystemEvents` fires on Suspend / Resume / StatusChange. We log all three at Warning level. The handler runs on a background thread inside `SystemEvents`'s own message-pump, so the work must be tiny — a single Serilog call, then return.

Skeleton inside [TerminationDiagnostics.Install](CodeWorker/Logging/TerminationDiagnostics.cs#L8):

```csharp
public static void Install()
{
    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    Console.CancelKeyPress += OnCancelKeyPress;

    RegisterPosixSignal(PosixSignal.SIGTERM);
    RegisterPosixSignal(PosixSignal.SIGINT);
    RegisterPosixSignal(PosixSignal.SIGHUP);
    RegisterPosixSignal(PosixSignal.SIGQUIT);

    RegisterPowerEvents();
}

private static void RegisterPowerEvents()
{
    if (!OperatingSystem.IsWindows())
    {
        return;
    }

    SystemEvents.PowerModeChanged += OnPowerModeChanged;
}

[SupportedOSPlatform("windows")]
private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
{
    Log.Warning("Power mode change {Mode}", args.Mode);

    if (args.Mode == PowerModes.Suspend)
    {
        Log.CloseAndFlush();
    }
}
```

Two important details:

1. **Flush on Suspend.** When the Suspend event fires, the OS gives the process a short window before it actually pauses the thread. The handler must call `Log.CloseAndFlush()` so the Suspend line is on disk before the machine sleeps — otherwise it sits in a buffered sink and is lost if the sink rolls or the process never wakes cleanly.
2. **`[SupportedOSPlatform("windows")]`** on the handler — `SystemEvents` is platform-gated in .NET 6+. Without the attribute, build emits CA1416 warnings.

### Why not a new sibling class?

`TerminationDiagnostics` is already "the class that subscribes to OS events that might explain the process stopping." Adding a `PowerEventDiagnostics` sibling would split a single-responsibility ("log the things that take us down") into two classes that get installed together. Keep it in one place; one `Install()` call from `Program.Main`.

### Why log Resume / StatusChange too?

- **Resume** is the breadcrumb that says "we came back from sleep" — useful when the runner is supposed to continue running across a wake (e.g. a scheduled overnight run that the screen-sleep policy interrupted). Without it, post-mortem cannot tell "process kept running through suspend" from "process restarted manually."
- **StatusChange** (battery transitions, AC plug/unplug) is rare but cheap to log. Worth keeping just because the handler shape is identical.

If the volume becomes annoying we can downgrade Resume / StatusChange to `Information` later. Start at `Warning` so they show up in every default log filter.

## TDD Plan

Tests live under `CodeWorker.Tests` mirroring source folders. Use `xUnit` + `FakeItEasy` + `FluentAssertions`. Write tests **before** implementation.

### Testing strategy

`SystemEvents.PowerModeChanged` is a static event on a sealed framework type. We cannot raise it from a test without OS cooperation. Two practical seams:

1. **Wiring test.** Verify `Install()` does not throw on Windows and registers a non-null handler. Use reflection on `SystemEvents` to confirm the event has at least one subscriber after `Install()` runs. This is a smoke test — confirms the subscribe call ran without throwing on the test host.
2. **Behavioural test via a small wrapper.** Extract the actual `OnPowerModeChanged` body into a method that takes a `PowerModes` value and an `ILogger`, then call that method directly from tests. The `SystemEvents` subscription becomes a one-liner that forwards `args.Mode` and a logger reference into the testable method. This keeps the testable surface independent of the framework event.

Prefer option 2 — it makes the actual logging behaviour testable without reflection trickery. The static handler delegates to a small `PowerEventLogger` (or equivalent name) that takes the mode and writes to Serilog.

### `PowerEventLoggerTests` (or whichever name the extracted helper gets)

1. `LogSuspendAtWarningLevel` — pass `PowerModes.Suspend`; assert a single Warning log entry containing `"Suspend"`.
2. `LogResumeAtWarningLevel` — pass `PowerModes.Resume`; assert a single Warning log entry containing `"Resume"`.
3. `LogStatusChangeAtWarningLevel` — pass `PowerModes.StatusChange`; assert a single Warning log entry.
4. `FlushSerilogOnSuspend` — assert the suspend path triggers a flush (use a fake `ILogFlusher` or equivalent indirection; or assert via an in-memory sink that the line is persisted before return).
5. `DoNotFlushOnResume` — assert no flush on Resume / StatusChange.

### `TerminationDiagnosticsTests`

If the file already exists, add:

6. `SubscribeToPowerModeChangedOnWindows` — call `Install()` on a Windows test host; assert (via reflection on `SystemEvents.PowerModeChanged`'s backing delegate) that at least one subscriber is registered. Gate the test with `[SkippableFact]` or an `OperatingSystem.IsWindows()` check that no-ops on non-Windows agents.
7. `DoNotThrowOnNonWindowsHosts` — call `Install()` with a fake `IOperatingSystem` indicating non-Windows; assert no exception, and that `SystemEvents.PowerModeChanged` was not touched.

For (7), introduce a thin `IOperatingSystem.IsWindows()` seam so the test is deterministic, or accept that this test only runs on a Linux CI agent if such an agent exists. Pick the simpler option that matches the existing platform-gating pattern in this codebase.

## Implementation Order

**Phase 1 — Extracted helper, tested**
1. Add `PowerEventLoggerTests` covering cases 1–5. Red.
2. Create [CodeWorker/Logging/PowerEventLogger.cs](CodeWorker/Logging/PowerEventLogger.cs) (or fold into `TerminationDiagnostics` as a private static method — pick whichever the test seam needs). Green.

**Phase 2 — Wire into TerminationDiagnostics**
3. Add the `Microsoft.Win32.SystemEvents` package reference to [CodeWorker.csproj](CodeWorker/CodeWorker.csproj). `dotnet restore`.
4. Add `RegisterPowerEvents()` to [TerminationDiagnostics.Install](CodeWorker/Logging/TerminationDiagnostics.cs#L8). Apply `[SupportedOSPlatform("windows")]` where the analyzer requires it. Confirm zero CA1416 warnings.
5. Add `TerminationDiagnosticsTests` cases 6–7. Green.

**Phase 3 — Manual verification**
6. Run the runner; lock the screen / press Win+L / use `rundll32.exe powrprof.dll,SetSuspendState 0,1,0` to trigger Suspend (only when no real work is in flight). Confirm the log shows `Power mode change Suspend` immediately before the silence and `Power mode change Resume` on wake.
7. `dotnet format` → `dotnet build` (triggers CSharpier) → `dotnet test`.

## Constraints

- Do not introduce a cross-platform power abstraction. Gate at the call site with `OperatingSystem.IsWindows()`.
- Do not attempt to delay / cancel suspend. Windows does not give .NET processes a real veto over modern Connected-Standby suspends; trying to use `SetThreadExecutionState` or `PowerCreateRequest` is out of scope and a separate, much riskier design.
- Do not change [TerminationDiagnostics](CodeWorker/Logging/TerminationDiagnostics.cs)'s existing handlers. Only add to `Install()`.
- Follow every rule in `.claude/rules/csharp/` — no exceptions. Primary constructors where applicable, block-body methods, file-scoped namespaces, single class per file, switch expressions, no `Async` suffix without overload disambiguation, no `ConfigureAwait(false)`.

## Acceptance Criteria

- Running the runner and triggering a Windows Suspend (e.g. `rundll32.exe powrprof.dll,SetSuspendState 0,1,0`) produces a `[WRN] Power mode change Suspend` line in the log **before** the machine actually sleeps.
- The line is on disk after wake (`Log.CloseAndFlush()` ran inside the handler).
- Wake produces a matching `[WRN] Power mode change Resume` line.
- No CA1416 / platform-gating warnings introduced.
- On non-Windows hosts, `Install()` runs without touching `SystemEvents` and without throwing.
- All existing TerminationDiagnostics behaviour (UnhandledException, UnobservedTaskException, ProcessExit, CancelKeyPress, POSIX signals) is unchanged.

Build:
- `dotnet build`, `dotnet test`, `dotnet format` all clean.

## Verification

- [ ] Tests written before implementation (TDD)
- [ ] No compiler warnings introduced (including CA1416)
- [ ] Namespaces match folder paths exactly
- [ ] Must follow all rules `.claude\rules\csharp` no exceptions
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] `dotnet build` to apply CSharpier changes
- [ ] Manually triggered Windows Suspend writes a log line before sleep and a matching Resume line after wake
- [ ] Report results before finishing
