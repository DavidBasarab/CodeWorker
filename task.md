# Task

<!-- One-sentence description of what this task accomplishes -->

## Feature Context

Going to run the application tasks.  This will be from the appsettings.json.  The flow is going to be read the repositories from the appsettings.json.  Pull a task from the `todo` folder.  Move it into the `pending` folder.  Have claude do the work.  Once claude is done commit the change.  Move the task from `pending` to `done`. 

## Tasks

- [ ] Read the repository from settings.
- [ ] Read the repository settings.
- [ ] If there as `todo` task take it for work
- [ ] Pull them in order of name like `11_MyTask` and them `14_ThisTask` if there are not `Number_` assume alphabetical.  If there is only one then order does not matter.
- [ ] When taking for work that means moving to `pending` folder
- [ ] Log what task is starting by name of the task file
- [ ] Have claude do the task
- [ ] Wait for claude to finish
- [ ] Log the result of the task to the a log file in the repository `CodeWorker.log`
- [ ] Move the folder from `pending` to complete
- [ ] Take the next task and loop until complete

## Required Steps

1. Run `dotnet format` on all created/modified `.cs` files
2. Run `dotnet build` to apply CSharpier changes

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

- Ready the `README.md` for an overview of the application
- <!-- Related issue, PR, or ticket -->
- <!-- Relevant existing classes or files Claude should read first -->

## Notes

- <!-- Constraints, decisions, known edge cases -->

