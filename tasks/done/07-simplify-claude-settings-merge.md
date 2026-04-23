# Simplify ClaudeSettings.MergeWith

## Goal
`MergeWith` in [CodeWorker/Settings/ClaudeSettings.cs](CodeWorker/Settings/ClaudeSettings.cs) duplicates every field assignment twice — once in the `overrides == null` branch (which is effectively a clone), and once in the merge branch. Seven fields × two branches = 14 assignments for what is conceptually "take the override if it is set, else keep the current value."

## Target shape
A single return statement with per-field ternaries handles both cases. The `overrides == null` check becomes redundant once you short-circuit each field check on `overrides?.Foo`:

```csharp
public ClaudeSettings MergeWith(ClaudeSettings overrides)
{
    return new ClaudeSettings
    {
        Model            = string.IsNullOrEmpty(overrides?.Model)            ? Model            : overrides.Model,
        MaxTurns         = overrides?.MaxTurns > 0                            ? overrides.MaxTurns : MaxTurns,
        SkipPermissions  = overrides?.SkipPermissions ?? SkipPermissions,
        OutputFormat     = string.IsNullOrEmpty(overrides?.OutputFormat)     ? OutputFormat     : overrides.OutputFormat,
        SystemPromptFile = string.IsNullOrEmpty(overrides?.SystemPromptFile) ? SystemPromptFile : overrides.SystemPromptFile,
        AllowedTools     = overrides?.AllowedTools is { Count: > 0 }         ? overrides.AllowedTools : AllowedTools,
        TimeoutMinutes   = overrides?.TimeoutMinutes > 0                     ? overrides.TimeoutMinutes : TimeoutMinutes,
    };
}
```

Note: the current code's `overrides == null` branch returns a clone that preserves all `SkipPermissions` values including `false`. The merge branch unconditionally takes `overrides.SkipPermissions`. The new shape must preserve these exact semantics — verify carefully against `ClaudeSettingsMergeTests`.

## Pitfalls to watch
- Nested ternaries are discouraged by the rules — the shape above is one level deep per line, which is acceptable, but if clarity suffers, extract per-field helpers.
- Project rules say to prefer clear `if` statements over complex nested ternaries. Use judgment: if the single-return shape reads cleanly, keep it; otherwise, leave the method as-is and close the task as "not worth it."

## Tests
All existing tests in `ClaudeSettingsMergeTests` must pass unchanged.

## Definition of done
- Method is shorter than today OR a written justification for leaving it alone.
- All merge tests pass; CSharpier clean.
