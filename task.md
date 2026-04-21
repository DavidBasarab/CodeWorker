
## Feature Context

When `FatCatCodeWorker setup` is done the repositories that is added to the track repository list.

## Task

- [ ] on `setup` add the repository it is run in to the repository the app is tracking
- [ ] Add a `untrack` method that will remove that repository from the app that it is tracking
- [ ] Add a `list` command line parameter that will list all the repository that are being tracked
- [ ] On the `untrack` if no other parameter is given it should remove the repository it is run in else it should take a name form the list and remove it.
- [ ] Add a method called `track` that will run in a repository that is already setup or by name that will add it to the tracked list

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

