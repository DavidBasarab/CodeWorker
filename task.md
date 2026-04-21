
## Feature Context

**Failed vs Blocked distinction** — README defines both states but only Blocked (non-zero exit) is implemented. No separation between infrastructure failures and logical block `ers

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

## Notes

- <!-- Constraints, decisions, known edge cases -->

