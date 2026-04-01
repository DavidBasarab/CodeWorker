
## Feature Context

Validate repository before doing any work.

## Tasks

- [ ] Add validation interface
- [ ] Call validation before doing any work
- [ ] if validation fails then make the repository as broken and the user will have to clear
- [ ] If one repository is broken the others that are not broken can be run

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

