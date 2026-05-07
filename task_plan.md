# Plan: Survive Parent-Process Death And Reliably Capture Claude's Work

## What Actually Happened

Re-reading the run with the corrected fact that **the user did not kill anything**:

- `21:27:26.282` — child Claude PID 58388 started.
- `21:27:56 / 21:28:26 / 21:28:56` — three 30s heartbeats fired, each reporting `stdoutBytes=0, stderrBytes=0`.
- `21:28:56` — last log line. Both console *and* the Serilog file log end here.
- `~21:29:26` — shell prompt returns showing total dotnet runtime `2m 0.025s`.
- **No** `Process exited` log. **No** `Claude exited with code` log. **No** `Task runner complete` log. **No** exception printed by `Program.Main`'s catch. **No** Serilog `CloseAndFlush` aftermath even though `Program.Main` registers it on `AppDomain.CurrentDomain.ProcessExit` and Serilog's file sink flushes every 1s.

That pattern means the .NET host did not unwind `Program.Main` at all. It died abnormally — a native crash (`StackOverflowException`, `AccessViolationException`), an `Environment.FailFast`, a `CTRL_CLOSE_EVENT`-style console signal, or an external kill (AV, OS, Task Manager). We do not currently know which, because we have **no diagnostics installed for any of those paths**.

Meanwhile the child `claude` process probably continued (Windows children with `UseShellExecute=false` are not auto-killed when the parent dies), eventually finished or got reaped, and its output went to `/dev/null` because the only consumer was the now-dead .NET process's stdout pipe. That is why the live log is 0 bytes and the JSON result is gone forever — not because Claude was silent, but because the ear that was listening stopped existing.

## What The Previous Plan Got Wrong

It assumed the bug was Claude buffering output (`--output-format json`) and the parent waiting too patiently. That is *a* problem — the live log being empty in JSON mode is real — but it is not what killed this run. Even if we switch to `stream-json`, if the parent dies mid-await we lose everything the same way. The fix has to start with: **the parent's death must not destroy the run's output, and we must know why the parent died.**

## Goals (Revised, In Priority Order)

1. **Claude's output survives parent death.** Output streams to a file on disk, written by Claude's own stdout, not by a .NET stream reader.
2. **Diagnose parent death.** Install handlers for every termination path .NET can observe and log to the file sink with `Flush()` before any of them return.
3. **Recover on next startup.** Tasks left in `pending/` with a finalized transcript get classified and moved without re-invoking Claude.
4. **Detect real hangs.** Idle-timeout based on transcript file growth, not on a pipe byte counter that's always zero.
5. **Stop the parent from being responsible for the bytes.** The .NET process becomes a tailer of an on-disk file, not a pipe consumer. This removes whatever's currently killing it from the critical path.
6. **Stream-json everywhere.** Required for goal 1 to be useful — JSON mode would only land a single blob at the very end, defeating the whole approach.

## Architecture: Orchestrator + Detached Worker

```
┌──────────────────┐     spawns      ┌────────────────────┐
│   CodeWorker     │ ─ ─ ─ ─ ─ ─ ─ ▶ │  PowerShell        │
│  (.NET host)     │  detached       │  Run-ClaudeTask.ps1 │
│                  │                 │     │              │
│  tails           │                 │     ▼              │
│  transcript.jsonl│ ◀── writes ──── │  claude … …jsonl   │
│  + .done sentinel│                 │  + writes sentinel │
└──────────────────┘                 └────────────────────┘
```

- The wrapper PowerShell script is responsible for invoking Claude with stdout redirected to `<task>.transcript.jsonl` and stderr to `<task>.stderr.log`. It is **detached from CodeWorker's process tree** (no inherited pipes).
- On Claude's exit, the wrapper appends a single `{"type":"orchestrator-done","exitCode":N,"endedAt":"…"}` line to the transcript and writes a `.done` sentinel file next to it. These are CodeWorker's two unambiguous signals that the run is complete.
- CodeWorker tails the transcript file (polling every ~250ms, position-tracked). It does **not** hold pipes to Claude. If CodeWorker dies, the wrapper and Claude keep going; the file accumulates regardless.
- On startup, before discovering new tasks, CodeWorker scans `pending/` for any `<task>.transcript.jsonl`. If a `.done` sentinel exists, classify and move. If the transcript exists but no sentinel and no live wrapper, mark `Stalled` and route per repo settings. If the wrapper is still running, attach by tailing.

## Concrete Changes

### 1. New: `Run-ClaudeTask.ps1`

Lives in `CodeWorker/Claude/Scripts/Run-ClaudeTask.ps1`, embedded as a resource and copied into `tasks/.codeworker/` on first run so it can be edited without rebuilding.

Parameters:
- `-PromptFile <path>` (full prompt text)
- `-TranscriptFile <path>` (NDJSON output)
- `-StderrFile <path>`
- `-DoneSentinel <path>`
- `-ClaudeArgs <string[]>` (model, max-turns, system prompt, tools, etc.)

Body (sketch — not final code, just shape):

```powershell
. $PROFILE
$ErrorActionPreference = "Stop"

# Stdout → transcript, stderr → stderr file
& claude @ClaudeArgs --output-format stream-json --verbose --input-file $PromptFile `
    1>> $TranscriptFile 2>> $StderrFile

$exit = $LASTEXITCODE
$done = @{ type = "orchestrator-done"; exitCode = $exit; endedAt = (Get-Date).ToString("o") } | ConvertTo-Json -Compress
Add-Content -LiteralPath $TranscriptFile -Value $done
Set-Content -LiteralPath $DoneSentinel -Value $exit
```

CodeWorker invokes the script via:

```
pwsh -NoProfile -NonInteractive -WindowStyle Hidden -File Run-ClaudeTask.ps1 …
```

…using `Process.Start` with `UseShellExecute=true`, **no redirected pipes**, and `CreateNoWindow=true`. That is the configuration where the child is genuinely independent of the parent's std handles.

### 2. New: `IClaudeTranscriptTailer`

- Polls the transcript file at a configurable interval (default 250ms).
- Maintains a byte offset; reads only new bytes; splits on `\n`; parses each as a `ClaudeStreamEvent`.
- Emits events to a `ClaudeProgressTracker` (counters + `lastEventAt`).
- Returns when:
  - a `result` event is seen, **or**
  - an `orchestrator-done` sentinel line is seen, **or**
  - the `.done` file appears, **or**
  - idle timeout elapses with no new bytes (default 10 minutes), **or**
  - wall-clock timeout elapses (existing 90-minute setting).

### 3. New: `ClaudeStreamEvent` (NDJSON discriminated union)

`system | assistant | user | tool_use | tool_result | result | orchestrator-done`. Matches the documented `stream-json --verbose` schema for everything except the orchestrator sentinel, which the wrapper script owns.

### 4. New: `IRecoverPendingTasks`

Runs at the top of `RunCommand.Execute`, before `processRepository.Process`. For each repo:

- For each `*.transcript.jsonl` in `pending/`:
  - If `<task>.done` exists → parse the transcript, classify, move task to `done/`/`failed/`/`blocked/` per outcome, move the transcript and stderr alongside it.
  - Else, if a wrapper PID file exists and the PID is alive → attach the tailer and continue this run.
  - Else → mark `Stalled`, route per `RepositorySettings.OnStalled` (new setting; default `blocked/`), preserve transcript for postmortem.

### 5. Diagnostics For Why The Parent Died

Add to `Program.Main`, before `application.DoWork`:

```csharp
AppDomain.CurrentDomain.UnhandledException += (_, e) => {
    Log.Fatal(e.ExceptionObject as Exception, "UnhandledException IsTerminating={IsTerminating}", e.IsTerminating);
    Log.CloseAndFlush();
};
TaskScheduler.UnobservedTaskException += (_, e) => {
    Log.Error(e.Exception, "UnobservedTaskException Observed={Observed}", e.Observed);
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => {
    Log.Information("ProcessExit fired");
    Log.CloseAndFlush();
};
Console.CancelKeyPress += (_, e) => {
    Log.Warning("CancelKeyPress received SpecialKey={Key} Cancel={Cancel}", e.SpecialKey, e.Cancel);
};
PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => Log.Warning("SIGTERM"));
PosixSignalRegistration.Create(PosixSignal.SIGHUP,  ctx => Log.Warning("SIGHUP"));
PosixSignalRegistration.Create(PosixSignal.SIGINT,  ctx => Log.Warning("SIGINT"));
```

If next run dies the same way, we'll see at least which path fired (or confirm none did, which narrows it to a native crash and points at AV/OS as the likely killer).

Also lower Serilog file sink `flushToDiskInterval` from `1s` to `250ms`, and call `Log.CloseAndFlush()` in a `Task.Delay`-based watchdog every 5s during long awaits — cheap insurance against losing the last second of logs to a hard kill.

### 6. Settings

```jsonc
"Claude": {
  "Model": "claude-opus-4-6",
  "MaxTurns": 100,
  "SkipPermissions": true,
  "OutputFormat": "stream-json",       // was "json"
  "TimeoutMinutes": 90,
  "IdleTimeoutMinutes": 10,            // new
  "TranscriptPollMilliseconds": 250    // new
}
```

`OutputFormat` is no longer user-tunable in practice — anything other than `stream-json` breaks the tailer — but kept in settings for diagnostics.

### 7. Files Touched / Added

| File | Change |
|------|--------|
| `Claude/Scripts/Run-ClaudeTask.ps1` | **new** — embedded resource, copied to `tasks/.codeworker/` |
| `Claude/ClaudeRunner.cs` | rewritten: writes prompt + args to disk, launches detached pwsh wrapper, hands off to tailer |
| `Claude/ClaudeStreamEvent.cs` | **new** |
| `Claude/ClaudeTranscriptTailer.cs` | **new** |
| `Claude/ClaudeProgressTracker.cs` | **new** |
| `Claude/RecoverPendingTasks.cs` | **new** |
| `Settings/ClaudeSettings.cs` | add `IdleTimeoutMinutes`, `TranscriptPollMilliseconds`; default `OutputFormat = "stream-json"` |
| `Commands/Run/ClassifyTaskResult.cs` | consume the `result` event first; text heuristics fall back only when no `result` event exists |
| `Commands/Run/RunCommand.cs` | call `IRecoverPendingTasks` before processing new tasks |
| `Program.cs` | install diagnostic handlers from §5 |
| `Process/RunProcess.cs` | unchanged (still used for `claude --version`, git) |

### 8. TDD

- `ClaudeTranscriptTailerTests`: feeds a fake filesystem with NDJSON appended over time. Asserts events are surfaced in order, `result` event ends the wait, idle timeout triggers, partial trailing line is buffered until newline.
- `RunClaudeTaskScriptTests` (PowerShell-free unit): verifies the C# code that **assembles** the pwsh command line — argument escaping, paths quoted, no inherited pipes flag set. The script itself is small enough that it doesn't need its own test (per `powershell.md`).
- `RecoverPendingTasksTests`: pending task with `.done` + success transcript → moved to `done/`. With `error_max_turns` → `blocked/`. No sentinel + no live wrapper → `Stalled` → routed per setting.
- `ClassifyTaskResultTests` extended for `result`-event-driven classification with text-heuristic fallback.
- `ClaudeRunnerTests`: launches via wrapper script path, never sets `StandardInput`, never reads pipes, returns immediately after handing off to the tailer.
- `ProgramDiagnosticsTests`: smoke test that the handlers from §5 are wired (resolved via a small `ITerminationDiagnostics` registrar so it's testable).

### 9. Migration Notes

- First run after upgrade: `tasks/.codeworker/` directory is created, `Run-ClaudeTask.ps1` written there, `appsettings.json` schema upgraded in-place (preserving user values, adding new keys with defaults).
- Existing `pending/<task>.live.log` files from the old format are renamed to `<task>.legacy.live.log` and ignored by the recovery scan.

## Why This Survives The Failure We Just Saw

Today's run: parent dies → child orphaned → child's stdout pipe goes nowhere → output lost → task left in `pending/` with a 0-byte log → next run can't tell what happened.

After this plan: parent dies → wrapper + Claude keep running → transcript file grows → `.done` sentinel written → next CodeWorker startup sees the sentinel, classifies, moves the task. The recovery path is the *primary* path for any run where the parent doesn't make it to the end, not a special case.

## Open Questions Worth Confirming Before Coding

1. Does `pwsh -WindowStyle Hidden -File … &` (started via `Process.Start` with `UseShellExecute=true`) actually survive the .NET parent's death on Windows in practice? I believe yes, but worth a 5-minute manual verification before committing to the design.
2. The "what killed the parent" diagnostic in §5 may turn up evidence (e.g. a SIGINT-like console signal from the IDE, or an unhandled exception we're currently swallowing) that lets us also fix the root cause. Worth landing §5 first as a small standalone PR.
3. `claude` may not support `--input-file`. If the only non-interactive input path is stdin, the wrapper script must pipe the prompt file in via PowerShell `Get-Content $PromptFile | claude …`. Verify with `claude -h` before finalizing.

## Out Of Scope

- Parallel task execution.
- Changing the markdown task format.
- Anything in the git workflow.
