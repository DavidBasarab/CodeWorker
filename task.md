
## Feature Context

**Claude CLI config passthrough** — Settings define `Model`, `MaxTurns`, `TimeoutMinutes`, `AllowedTools`, `OutputFormat`, but `ClaudeRunner` only uses `-p --input-file`. Most config options aren't passed as CLI args

## Tasks

- [x] Create a way to pass config items
- [x] Only include the items that is given, if not value is defined then let claude use its default
- [x] The user define settings always rules but if not then use the application default settings
- [x] Log what settings was used for each task.

## Required Steps

1. Do all tasks
2. Run all verification tests

## Verification

- [x] Tests written before implementation (TDD)
- [x] All tests pass (`dotnet test`)
- [x] `dotnet format` run on all modified files
- [x] `dotnet build` to apply CSharpier changes
- [x] No compiler warnings introduced
- [x] Namespaces match folder paths exactly
- [x] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [x] Report results before finishing

## References

- Read AI_CONTEXT.md before doing anything.
- C:\Code\CodeWorker\CodeWorker\appsettings.json
- 

## Notes

- <!-- Constraints, decisions, known edge cases -->

