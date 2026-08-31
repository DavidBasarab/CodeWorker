# CodeWorker Coding Standards

This file defines the coding standards for the CodeWorker codebase.
All code you generate — in any context — must follow the rules for the relevant language below.
The goal is that AI-generated code is indistinguishable from code written by a senior member of this team.

## ⚠️ READ THIS FIRST — Before Any Work

**Before doing anything else in this repository — reading, planning, or writing code — read [`README.md`](README.md) in full.**
It is the single source of truth for what this tool is and how it is built. Do not skip it, even for a "small" change.

The README tells you the things you must know before you touch a line of code:
- **Product:** CodeWorker (the *Claude Overnight Task Runner*) — a Windows CLI tool that processes queued coding tasks in one or more local repositories using Claude Code, commits each completed task as a separate Git commit, and prepares the results for human review the next morning. Understanding the domain (tracked repository, task queue, `todo`/`done`/`blocked`/`pending`/`reference` folders, task outcome, run history, heuristics) is required to name and place code correctly.
- **Tech stack:** **.NET 10 / C#** built on the **FatCat** toolkit. Dependency injection is Autofac via `SystemScope`; logging is **Serilog** (`ILogger`) with `ConsoleLog` for console output. There is **no web server, no database, no UI** — it is a console executable (`FatCatCodeWorker`) driven by command-line arguments. **PowerShell** scripts handle install and automation (`Install-CodeWorker.ps1`, the embedded `Run-ClaudeTask.ps1`).
- **Architecture:** the CLI command pattern — `args[0]` selects an `ICommand`. `CodeWorkerApplication` resolves the command through `IResolveCommand` / `CommandResolver` and executes it. A new command is a new folder under `Commands/` with a capability interface extending `ICommand`, registered by Autofac automatically.

If anything below in the coding standards appears to conflict with the README, the README describes *what* the tool is; these rules describe *how* to write code for it — follow both. Where the README and the actual code disagree, **the code is authoritative** — trust the code and flag the stale README.

## Repository Layout

Production projects with mirrored test projects, all in `CodeWorker.sln` (legacy `.sln` format) at the repo root:

| Path | Project | Purpose |
|---|---|---|
| `CodeWorker` | `CodeWorker` (`FatCatCodeWorker`) | The console executable — command resolution, task discovery and processing, git workflow, run history, logging. The application. |
| `CodeWorker.Cli` | `CodeWorker.Cli` | Command-line interface surface (added for future use). |
| `CodeWorker.Tests` | `CodeWorker.Tests` | Mirrors `CodeWorker` one-for-one. |
| `CodeWorker.Cli.Tests` | `CodeWorker.Cli.Tests` | Mirrors `CodeWorker.Cli` one-for-one. |

All production namespaces start with `FatCat.CodeWorker.*`; test namespaces start with `Testing.FatCat.CodeWorker.*`.

Everything must build and every test must pass before work is considered done:
```bash
dotnet build CodeWorker.sln
dotnet test CodeWorker.sln
```

---

## C# Rules

Apply these rules to all C# code. Do not apply them to PowerShell, TypeScript, or any other language.

@.claude/rules/csharp/naming-and-structure.md
@.claude/rules/csharp/types-and-di.md
@.claude/rules/csharp/toolchain.md
@.claude/rules/csharp/async.md
@.claude/rules/csharp/errors-and-logging.md
@.claude/rules/csharp/testing.md
@.claude/rules/csharp/not-allowed.md

## PowerShell Rules

Apply these rules to all PowerShell scripts. Do not apply them to C#, TypeScript, or any other language.

@.claude/rules/powershell/powershell.md

## TypeScript & React Rules

Apply these rules to all TypeScript and TSX files. Do not apply them to C#, PowerShell, or any other language.

@.claude/rules/typescript/naming-and-structure.md
@.claude/rules/typescript/toolchain.md
@.claude/rules/typescript/async.md
@.claude/rules/typescript/react.md
@.claude/rules/typescript/i18n.md
@.claude/rules/typescript/errors.md
@.claude/rules/typescript/performance.md
@.claude/rules/typescript/forms.md
@.claude/rules/typescript/datamigration.md
@.claude/rules/typescript/testing.md
@.claude/rules/typescript/not-allowed.md
