# Task

Add settings file and more settings.

## Feature Context

I want to be able to configure the application on what repositories to read.  This will be the settings filed defined in `Repository Configuration` of the ReadMe

## Tasks

- [ ] Create `defaultSettings.json` in C:\Code\CodeWorker\CodeWorker\Commands\Setup
- [ ] The defaultSettings.json is found in the `README.md`
- [ ] Embeed `defaultSettings.json` on setup write the `settings.json` file.
- [ ] On SetUp write the `settings.json` file.
- [ ] Add `pending` folder on setup
- [ ] Add `blocked` folder on setup

## Required Steps

1. Run `dotnet format` on all created/modified `.cs` files

## Verification

- [ ] Tests written before implementation (TDD)
- [ ] All tests pass (`dotnet test`)
- [ ] `dotnet format` run on all modified files
- [ ] No compiler warnings introduced
- [ ] Namespaces match folder paths exactly
- [ ] No banned patterns used (see `.claude/rules/not-allowed.md`)
- [ ] Report results before finishing

## References

- Ready the ReadME.md for an overview of the application
- C:\Code\CodeWorker\CodeWorker\Commands\Setup

## Notes

