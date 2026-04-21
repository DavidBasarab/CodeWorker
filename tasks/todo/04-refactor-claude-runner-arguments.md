# Refactor ClaudeRunner.BuildArguments

## Goal
Break up the 46-line `BuildArguments` method in [CodeWorker/Claude/ClaudeRunner.cs](CodeWorker/Claude/ClaudeRunner.cs). Today it's a flag-builder with seven sequential `if` blocks, each checking a setting and appending to a `StringBuilder`. That's a conditional-append code smell and each branch is logically independent.

## Problems to fix
1. **One method, many responsibilities** — the method decides which flags apply AND formats each flag. Those are two different concerns.
2. **Reference content building is buried** — `BuildReferenceContent` is called inside `BuildArguments` but does a meaningful amount of work (StringBuilder loop, escape quotes, trim). It's a distinct capability.
3. **Logger chatter at the top of `Run`** — two large `logger.Information(...)` calls that really want to be one method: `LogClaudeStartup`.

## Target design

### Option A — small private helpers (preferred for this codebase)
Keep the endpoint-style pattern already in use elsewhere: one class, small private helpers that each append one section. `BuildArguments` becomes a list of ~8 method calls against a `StringBuilder` field:

```csharp
private StringBuilder arguments;

private string BuildArguments(string markdownFilePath, ClaudeSettings settings, List<ReferenceFile> refs)
{
    arguments = new StringBuilder();
    AppendInputFile(markdownFilePath);
    AppendModel(settings);
    AppendMaxTurns(settings);
    AppendOutputFormat(settings);
    AppendSystemPromptFile(settings);
    AppendSkipPermissions(settings);
    AppendAllowedTools(settings);
    AppendReferenceFiles(refs);
    return arguments.ToString();
}
```

Each helper is 3–5 lines with a single guard clause. This matches the pattern used in `ProcessTask` and `RunGitWorkflow` after the recent refactor.

### Option B — polymorphic flag list (only if the flag set will grow)
Introduce `IClaudeArgument` with `bool Applies(settings)` + `void AppendTo(builder, settings)`. Register each flag as a separate class. `BuildArguments` becomes a foreach over `IEnumerable<IClaudeArgument>`.

Pick Option A unless there's evidence the flag set will grow significantly. Over-engineering is explicitly called out in the project rules.

### Also extract
- `IBuildReferenceSystemPrompt` — distinct capability (formats the reference files as a system-prompt string, escapes quotes, trims). Today it's the private `BuildReferenceContent` helper. Making it injectable means it can be tested independently and swapped for tests of `ClaudeRunner` that don't care about reference formatting.
- `LogClaudeStartup` as a private helper in `ClaudeRunner` to collapse the two top-of-`Run` log statements.

## Tests
- Existing `ClaudeRunnerTests` must continue to pass.
- If `IBuildReferenceSystemPrompt` is extracted, write tests for it (format, escaping, trimming).
- Add argument-building tests that verify each flag appears only when its setting is populated, and that the flag ordering is stable.

## Definition of done
- No method in `ClaudeRunner` exceeds ~15 lines.
- `BuildReferenceContent` no longer lives inside `ClaudeRunner` (if extracted) — or if kept, it's a clearly named private helper reached through the new `AppendReferenceFiles` method.
- All tests pass; CSharpier clean.
