
## Feature Context

Going to run the application tasks.  After each claude process successfully completes we need to make a commit.  Look at the Readme.md for the format of the commit message.

## Tasks

- [ ] Add an interface to communicate with git.
- [ ] Should be able to make a commit, and push.  If there is a merge conflict then log it as a result and do nothing to resolve it.  The user should resolve it.
- [ ] Add to the readme and AI_CONTEXT the git rules
- [ ] Change when a a claude prompt is run to do a commit

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

