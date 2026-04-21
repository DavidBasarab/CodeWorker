# Refactor RunProcess

## Goal
Split [CodeWorker/Process/RunProcess.cs](CodeWorker/Process/RunProcess.cs) so that each responsibility lives in a cohesive helper. Today `Run` is 90 lines with four distinct responsibilities mashed together: configure `ProcessStartInfo`, wire stdout/stderr handlers, start the process (with failure path), and await exit (with optional timeout branch).

## Current status
The class is marked `[ExcludeFromCodeCoverage]` with the justification: *"Direct wrapper over System.Diagnostics.Process — no business logic, tested via IRunProcess fakes in consuming classes."*

This rationale is weaker than it looks — the timeout branch, the failed-to-start branch, and the process-kill-on-timeout are real branching logic, not just a thin wrapper. The refactor should either (a) reduce `RunProcess` to a true thin wrapper by extracting the branching into a separately tested class, or (b) remove the `[ExcludeFromCodeCoverage]` attribute and write tests for the extracted branching logic.

## Target design

### Extract these helpers as private methods on `RunProcess`
- `CreateStartInfo(settings)` — builds `ProcessStartInfo`. Pure, trivial.
- `WireOutputHandlers(process, result)` — hooks up `OutputDataReceived` / `ErrorDataReceived`.
- `TryStart(process, result)` — `process.Start()` with the try/catch that sets `FailedToStart`/`ExitCode = -1`/error line on failure. Returns `bool success`.
- `WaitForExit(process, result, settings)` — handles both the no-timeout and with-timeout branches, including the `Kill(entireProcessTree: true)` on timeout.

After extraction, `Run` should read top-to-bottom as: create start info → create process → wire handlers → try start → begin reads → wait for exit → set exit code → return.

### Consider pulling out timeout logic entirely
`IAwaitProcessWithTimeout` with one implementation that owns the `CancellationTokenSource`, the `WaitForExitAsync(token)` call, the `OperationCanceledException` catch, and the process-kill behavior. This is the most testable piece — timeouts are real business logic.

If extracted:
- `RunProcess` becomes a thin enough wrapper that `[ExcludeFromCodeCoverage]` is honest.
- `AwaitProcessWithTimeout` gets full unit-test coverage (timeouts, clean exits, kill behavior) using a fakeable `IProcess` abstraction.
- This may be overkill — use judgment. The smallest credible win is just the private-helper extraction.

## Tests
- `RunProcessTests` don't exist today (class is `[ExcludeFromCodeCoverage]`). If `AwaitProcessWithTimeout` is extracted, write tests for it.
- Existing consumer tests (`ClaudeRunnerTests` fakes `IRunProcess`) are unaffected.

## Definition of done
- `Run` method ≤ ~15 lines.
- Each extracted helper has a single clear responsibility.
- If timeout logic is extracted into its own class, that class has test coverage and is NOT marked `[ExcludeFromCodeCoverage]`.
- CSharpier clean.
