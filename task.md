# Task

Making the console application run with basic setup

## Feature Context

Making code worker run as a console application using the toolkit as a model.

## Tasks

- [ ] Based on the consoleRunning.md from the Toolkit set up the `Program.cs` to run
- [ ] Enable logging with that will log to a `Logs` folder on each run.
- [ ] Run the program with a basic logging statement of "Welcome to Code Worker" runs to both the log file and the console.
- [ ] Iterate as needed to ensure it works.  If you cannot get it working after 5 attempts do let me know so I can look at it.

## Required Steps

1. <!-- Step 1 -->
2. <!-- Step 2 -->
3. <!-- Step 3 -->
4. Run `dotnet format` on all created/modified `.cs` files

## Verification

- [ ] Tests written before implementation (TDD)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] always end with a `dotnet build` to ensure CSharpier will run
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] Report results before finishing

## References

- C:\Code\FatCat.Toolkit\src\consoleRunning.md

## Notes

- Logs should always be added too and not deleted unless manually done by a user.
- Logs should include the date time stamp of local time when the run was done.
- Unit tests are not required for basic infrastructure set up

