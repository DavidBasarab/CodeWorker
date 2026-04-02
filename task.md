
## Feature Context

Tasks move to `blocked/` but no structured Markdown explanation (task name, timestamp, reason, recommended fix) is generated

## Tasks

- [ ] Create a way to provide an explanation on why an item is blocked
- [ ] If Possible a recommend fix
- [ ] If the issue is from claude write whatever output claude had, for instance if the user runs out of tokens as an example that will only be known by claude
- [ ] This will be logged by the tool and in the user repository

## Required Steps

1. Do all tasks
2. Run all verification tests

## Verification

- [ ] Tests written before implementation (TDD)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] `dotnet build` to apply CSharpier changes
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] Report results before finishing

## References

- Read AI_CONTEXT.md before doing anything.
- <!-- Related issue, PR, or ticket -->
- <!-- Relevant existing classes or files Claude should read first -->

## Notes

- <!-- Constraints, decisions, known edge cases -->

