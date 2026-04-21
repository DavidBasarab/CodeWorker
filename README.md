# Claude Overnight Task Runner

> Purpose: A Windows-based overnight automation runner that processes queued coding tasks in one or more local repositories using Claude Code, commits each completed task as a separate Git commit, and prepares the results for human review the next morning.

---

## Overview

Claude Overnight Task Runner is a personal developer automation tool for executing coding tasks unattended overnight.

The workflow is:

1. During the day, the developer writes task files and places them in a repository queue.
2. Overnight, a scheduled Windows task launches the runner.
3. The runner processes queued tasks in deterministic order.
4. Claude Code executes each task against the repository.
5. The runner commits the result of each completed task separately.
6. Completed work is pushed and ready for review in the morning.

This project is intentionally designed to keep the human in control:

- The AI performs implementation work
- Git preserves history and reversibility
- The developer reviews all changes before merging

---

## Getting Started

### 1. Install

Run the install script from PowerShell. It clones the repo, builds a release, publishes to `C:\Tools\CodeWorker` (by default), and adds that folder to your user PATH:

```powershell
pwsh -File Install-CodeWorker.ps1
```

If you don't have the repo locally yet, pull the script straight from GitHub:

```powershell
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/DavidBasarab/CodeWorker/main/Install-CodeWorker.ps1" -OutFile "$env:TEMP\Install-CodeWorker.ps1"
pwsh -File "$env:TEMP\Install-CodeWorker.ps1"
```

Open a new terminal after install so the updated PATH is picked up, then verify:

```powershell
FatCatCodeWorker --help
```

See [Installation on Windows](#installation-on-windows) for prerequisites, custom install paths, and updates.

### 2. Set Up a Repository

Because `FatCatCodeWorker` is on your PATH, you can run it from inside any repository. From the root of the repo you want the runner to manage:

```powershell
cd C:\Projects\my-api
FatCatCodeWorker setup
```

This creates the `tasks/` folder structure (`todo/`, `done/`, `blocked/`, `pending/`, `reference/`) and a default `tasks/settings.json`.

You can also target a repository explicitly without changing directory:

```powershell
FatCatCodeWorker setup C:\Projects\my-api
```

### 3. Register the Repository

Add the repository to the `Repositories` array in `appsettings.json` (in the install folder, e.g. `C:\Tools\CodeWorker\appsettings.json`):

```json
{
  "Repositories": [
    {
      "Path": "C:\\Projects\\my-api",
      "Enabled": true,
      "SettingsPath": "C:\\Projects\\my-api\\tasks\\settings.json"
    }
  ]
}
```

### 4. Queue a Task

Drop a Markdown task file into `tasks/todo/` in the repository (see [Task File Requirements](#task-file-requirements) for the template) and commit it. The next runner invocation will pick it up.

---

## Core Goals

### Primary Goal
Provide a reliable overnight coding assistant that can execute clearly defined repository tasks without supervision.

### Secondary Goals
- Preserve a clean Git history
- Support multiple repositories
- Make rollback simple
- Fail visibly instead of silently
- Keep task execution deterministic and auditable
- Make task instructions easy for AI systems to parse and follow

### Non-Goals
- Automatic merge to main
- Autonomous product decision-making
- Cross-task memory beyond repository files and current task context
- Replacing human review
- Handling vague or underspecified tasks without explicit assumptions

---

## Key Design Principles

### One task, one commit
Each successfully completed task should produce exactly one Git commit.

This is critical because it:
- makes review easier
- makes rollback predictable
- keeps intent aligned with the resulting code change

Never combine multiple tasks into one commit.

### Tasks must be self-contained
Each task file should include the information needed to complete the task safely.

Claude should not rely on memory from previous tasks. Every task should be executable independently using repository state plus repository instructions.

### Fail visibly
If a task cannot be completed safely, the system must record that clearly.

Preferred behavior:
- do not produce low-confidence changes silently
- create a blocked explanation
- move the task to `tasks/blocked/`
- continue or stop based on configuration

### Human review is required
The runner does not replace engineering judgment.

The developer must review all changes before merge or release.

### Prompts are versioned assets
Task files are part of the engineering process. They should live in source control and serve as:
- execution instructions
- historical record of intent
- documentation of why a change was requested

---

## Definitions

### Runner
The .NET console application that orchestrates overnight task execution.

### Task File
A Markdown file in `tasks/todo/` containing a single self-contained implementation request.

### Completed Task
A task that finished successfully, produced acceptable output, and was moved to `tasks/done/`.

### Blocked Task
A task that could not be completed safely or confidently and was moved to `tasks/blocked/` with an explanation.

### Pending Task
A task deferred for later processing, usually because it depends on another task, condition, or manual input.

### Repository Settings
Overrides stored in `tasks/settings.json` for repository-specific behavior.

### Reference Files
Supporting documents stored in `tasks/reference/` that may be included in task context.

---

## High-Level Workflow

A Windows Scheduled Task launches a .NET console application at a configured time, for example 2:00 AM.

For each configured repository, the runner performs the following steps:

1. Load global configuration
2. Load repository-level configuration overrides if present
3. Validate repository state and required folders
4. Pull the latest Git changes
5. Discover task files in `tasks/todo/`
6. Sort task files by filename in ascending order
7. Process each task one at a time
8. Execute Claude Code using the task file contents
9. Validate task completion outcome
10. Commit code changes if configured
11. Move the task file to the correct destination folder
12. Push changes if configured
13. Continue until all tasks are processed or stop conditions are met

Repository processing is sequential. The runner should not process multiple repositories in parallel unless that capability is intentionally added later.

---

## Repository Structure

Each monitored repository should follow this convention:

```text
/your-project
  /tasks
    /todo
      01-refactor-auth-service.md
      02-add-unit-tests-auth.md
      03-update-api-docs.md
    /done
      00-initial-setup.md
    /blocked
    /pending
    /reference
    settings.json
  /src
    ...
  CLAUDE.md
```

### Folder Meanings

- `tasks/todo/` — queue of tasks waiting to run
- `tasks/done/` — completed task files
- `tasks/blocked/` — tasks that could not be completed safely
- `tasks/pending/` — tasks intentionally deferred
- `tasks/reference/` — supporting files for AI context, examples, architecture notes, constraints, or standards
- `CLAUDE.md` — repository-level AI instruction file
- `tasks/settings.json` — repository-level overrides for runner behavior

---

## Execution Rules

The runner must follow these rules:

1. Process repositories in configured order
2. Within each repository, process task files in filename order
3. Execute only one task at a time per repository
4. Treat each task as isolated except for repository state and repository instructions
5. Do not skip numbered ordering unless explicitly configured
6. Do not continue a partially completed task silently
7. Record failures explicitly
8. Do not auto-merge branches
9. Do not rewrite Git history unless explicitly directed by a human
10. Prefer deterministic, reviewable changes over clever or risky changes

---

## Task File Requirements

Each task file should represent exactly one logical unit of work.

### Good Task Characteristics
- specific
- bounded
- testable
- self-contained
- references exact files, components, or patterns
- includes success criteria
- includes constraints

### Poor Task Characteristics
- vague
- open-ended
- product strategy questions
- multi-feature epics
- requests requiring unresolved human decisions
- tasks with no clear completion criteria

### Recommended Task Template

```markdown
# Task Title

## Objective
State exactly what should be changed.

## Scope
List the files, modules, layers, or components that are in scope.

## Requirements
- Requirement 1
- Requirement 2
- Requirement 3

## Constraints
- Do not change public APIs unless explicitly stated
- Preserve existing behavior unless explicitly stated
- Follow patterns already used in the repository

## Acceptance Criteria
- All relevant tests pass
- Build succeeds
- No unrelated files are modified
- Requested behavior is implemented

## Notes
Optional implementation hints, references, or examples.
```

### Example Task

```markdown
# Refactor Auth Service

## Objective
Refactor the authentication flow to use middleware-based token validation.

## Scope
- `/src/services/AuthService.cs`
- `/src/middleware/`
- `/src/controllers/UserController.cs`

## Requirements
- Extract token validation into `AuthMiddleware.cs`
- Remove duplicated validation logic from `UserController.cs`
- Preserve existing external behavior
- Follow the patterns established in `PaymentMiddleware.cs`

## Constraints
- Do not change the public interface of `AuthService`
- Do not modify unrelated controllers

## Acceptance Criteria
- Existing tests still pass
- Build succeeds
- Authentication behavior remains unchanged from the caller perspective
```

---

## AI Context Contract

This section exists specifically to help AI systems parse the project consistently.

### Assumptions About the AI Agent
The AI agent:
- has access to the local repository
- can read repository files
- can read `CLAUDE.md`
- can read the current task file
- may optionally read files from `tasks/reference/`
- does not retain reliable memory between tasks
- should make reasonable implementation decisions only within the boundaries of the task and repository conventions

### Required AI Behavior
The AI should:
- prioritize correctness over speed
- stay within task scope
- avoid unrelated refactors
- preserve existing conventions
- run or consider tests when appropriate
- document blockers clearly
- avoid asking clarifying questions during unattended execution
- make minimal necessary assumptions
- keep changes small and reviewable

### Prohibited AI Behavior
The AI should not:
- perform broad opportunistic refactors
- change unrelated files
- alter public APIs unless requested
- invent product requirements
- silently ignore blockers
- leave the repository in an ambiguous state

---

## Suggested Task State Model

Possible states:
- `Todo`
- `Running`
- `Done`
- `Blocked`
- `Pending`
- `Failed`

Suggested transitions:

```text
Todo -> Running -> Done
Todo -> Running -> Blocked
Todo -> Running -> Pending
Todo -> Running -> Failed
```

Folder mapping:
- `Todo` => `tasks/todo/`
- `Done` => `tasks/done/`
- `Blocked` => `tasks/blocked/`
- `Pending` => `tasks/pending/`

---

## Git Commit Strategy

Each successful task produces exactly one commit.

### Commit Principles
- one commit per task
- commit only after the task is complete
- commit message should make the task intent obvious
- commit should be reviewable on its own

### Example Commit Sequence

```text
commit a1b2c3  ←  01-refactor-auth-service
commit d4e5f6  ←  02-add-unit-tests-auth
commit g7h8i9  ←  03-update-api-docs
```

### Recommended Commit Message Format

```text
🤖 01-refactor-auth-service
```

Optional richer format:

```text
🤖 Task 01: refactor auth service
```

### Git Integration Rules

The runner performs git operations after each successful task:

1. **Commit**: If `Git.CommitAfterEachTask` is `true`, the runner stages all changes (`git add -A`) and commits with the message `{CommitMessagePrefix} {task-filename-without-extension}` (e.g. `🤖 01-refactor-auth-service`).
2. **Push**: If `Git.PushAfterEachTask` is `true`, the runner pushes to the remote after committing.
3. **Failure handling**:
   - If `git commit` fails, the runner **stops processing the current repository** immediately. No further tasks run against a repo with a failed commit.
   - If `git push` fails (merge conflict, auth error, network issue), the runner **stops processing the current repository** immediately. The failure is logged but no resolution is attempted.
   - Other configured repositories continue processing normally.
   - The user must resolve any git issues manually before the next run.
4. **Blocked tasks** do not produce commits or pushes — git operations only occur for successful tasks.

---

## Rollback Strategy

Rollback is intentionally simple because each task is isolated to one commit.

### Roll back everything from Task 2 onward

```bash
git reset --hard a1b2c3
```

Or preserve history:

```bash
git revert d4e5f6 g7h8i9
```

### Roll back only Task 2

```bash
git revert d4e5f6
```

If later tasks depend on Task 2, conflicts may occur.

### Best practice for dependent corrections
If Task 2 was wrong, revise the task prompt and re-queue it rather than manually patching a fragile chain of dependent commits.

---

## Global Configuration

The application reads `appsettings.json` at startup.

Global settings apply by default and may be overridden by repository-level settings.

```json
{
  "Repositories": [
    {
      "Path": "C:\\Projects\\my-api",
      "Enabled": true,
      "SettingsPath": "C:\\Projects\\my-api\\tasks\\settings.json"
    },
    {
      "Path": "C:\\Projects\\my-frontend",
      "Enabled": true,
      "SettingsPath": "C:\\Projects\\my-frontend\\tasks\\settings.json"
    }
  ],
  "Git": {
    "CommitAfterEachTask": true,
    "PushAfterEachTask": true,
    "PullBeforeEachTask": true,
    "CommitMessagePrefix": "🤖",
    "Branch": ""
  },
  "Claude": {
    "Model": "claude-sonnet-4-6",
    "MaxTurns": 10,
    "SkipPermissions": true,
    "OutputFormat": "json",
    "SystemPromptFile": "",
    "AllowedTools": [],
    "TimeoutMinutes": 30
  }
}
```

Notes:
- Repositories are processed in listed order
- Repository-level settings override global settings
- An empty `Branch` means use the currently checked out branch unless implementation defines otherwise

---

## Repository Configuration

Repository settings live at `tasks/settings.json`.

```json
{
  "Enabled": true,
  "LogResults": true,
  "Git": {
    "CommitAfterEachTask": true,
    "PushAfterEachTask": true,
    "PullBeforeEachTask": true,
    "CommitMessagePrefix": "🤖",
    "Branch": ""
  },
  "Claude": {
    "Model": "claude-sonnet-4-6",
    "MaxTurns": 10,
    "SkipPermissions": true,
    "OutputFormat": "json",
    "SystemPromptFile": "",
    "AllowedTools": [],
    "TimeoutMinutes": 30
  },
  "Tasks": {
    "TodoFolder": "tasks/todo",
    "DoneFolder": "tasks/done",
    "ReferenceFolder": "tasks/reference",
    "PendingFolder": "tasks/pending",
    "BlockedFolder": "tasks/blocked",
    "StopOnBlocked": true,
    "RunPlanningPhase": false
  },
  "Notifications": {
    "OnTaskComplete": false,
    "OnTaskFailed": true,
    "OnAllTasksComplete": true
  }
}
```

---

## Failure and Blocker Handling

The runner should distinguish between:
- execution failure
- blocked task
- incomplete task
- infrastructure failure

### Examples of Blocked Conditions
- missing dependency
- missing credentials
- contradictory task instructions
- required file does not exist
- tests reveal a larger issue outside task scope
- repository is in a broken state before execution starts

### Blocked Output Expectation
When blocked, create a Markdown file in `tasks/blocked/` that includes:
- task name
- timestamp
- reason blocked
- files examined
- recommended next step

Example:

```markdown
# Blocked: 02-add-unit-tests-auth

## Reason
Existing authentication tests are already failing on the current branch before task execution.

## Evidence
- `AuthTests.cs` fails in current HEAD
- Build fails before new changes are applied

## Recommended Next Step
Resolve the baseline test failure, then re-queue this task.
```

---

## Logging and Auditability

Recommended logging events:
- runner start
- repository start
- configuration loaded
- task discovered
- task started
- Claude invocation started
- Claude invocation finished
- validation result
- Git commit created
- Git push completed
- task moved to destination folder
- blocker detected
- repository completed
- runner completed

Recommended log fields:
- timestamp
- repository path
- task filename
- task state
- elapsed duration
- commit hash if created
- error details if present

---

## Installation on Windows

### Quick Install

Run the included install script from PowerShell. It clones the repo, builds, publishes, and adds the tool to your PATH:

```powershell
# Default install to C:\Tools\CodeWorker
pwsh -File Install-CodeWorker.ps1

# Or specify a custom install path
pwsh -File Install-CodeWorker.ps1 -InstallPath "D:\MyTools\CodeWorker"
```

If you don't have the repo locally yet, download and run the script directly:

```powershell
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/DavidBasarab/CodeWorker/main/Install-CodeWorker.ps1" -OutFile "$env:TEMP\Install-CodeWorker.ps1"
pwsh -File "$env:TEMP\Install-CodeWorker.ps1"
```

The script checks for prerequisites, clones to a temp folder, publishes a release build, and adds the output folder to your user PATH. Run it again at any time to update to the latest version.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) installed
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) installed and available on PATH
- Git installed and available on PATH

### Manual Build and Publish

From the repository root, publish a self-contained release build:

```powershell
dotnet publish CodeWorker/CodeWorker.csproj -c Release -o C:\Tools\CodeWorker
```

This produces `FatCatCodeWorker.exe` (and its dependencies) in `C:\Tools\CodeWorker`.

You can choose any output folder — `C:\Tools\CodeWorker` is used as an example throughout this README.

### Add to PATH

Add the publish folder to your system PATH so `FatCatCodeWorker` can be invoked from any command prompt:

```powershell
# Add to the current user's PATH permanently
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
[Environment]::SetEnvironmentVariable("Path", "$currentPath;C:\Tools\CodeWorker", "User")
```

After setting this, open a new terminal and verify:

```powershell
FatCatCodeWorker --help
```

### Configure

Copy or edit `appsettings.json` in the publish folder (`C:\Tools\CodeWorker\appsettings.json`) to point at your repositories. See the **Global Configuration** section below for the full schema.

For each repository, run the setup command to create the required `tasks/` folder structure:

```powershell
FatCatCodeWorker setup --path C:\Projects\my-api
```

### Update

To update after pulling new changes:

```powershell
dotnet publish CodeWorker/CodeWorker.csproj -c Release -o C:\Tools\CodeWorker
```

The same command overwrites the previous build in place.

---

## Windows Task Scheduler Setup

```powershell
$action = New-ScheduledTaskAction `
  -Execute "C:\Tools\CodeWorker\FatCatCodeWorker.exe"

$trigger = New-ScheduledTaskTrigger -Daily -At 2:00AM

$settings = New-ScheduledTaskSettingsSet `
  -ExecutionTimeLimit (New-TimeSpan -Hours 6) `
  -WakeToRun $true

Register-ScheduledTask `
  -TaskName "Claude Overnight Runner" `
  -Action $action `
  -Trigger $trigger `
  -Settings $settings `
  -RunLevel Highest
```

Notes:
- `WakeToRun` should be enabled if the machine may sleep overnight
- the execution time limit prevents runaway sessions
- production use should also consider working directory, credential context, and network availability

---

## Recommended CLAUDE.md Contract

Suggested `CLAUDE.md` content:

```markdown
# CLAUDE.md

## Execution Context
This repository may be modified by an overnight automation runner.

## General Rules
- Stay within the requested task scope
- Make the smallest reasonable change
- Preserve repository conventions
- Avoid unrelated refactors
- Prefer explicit and readable code
- Do not ask clarifying questions during unattended execution
- If blocked, explain the blocker clearly

## Quality Bar
- Build must succeed if buildability is expected for the task
- Relevant tests should pass
- Do not leave partial implementations silently

## Files and Scope
- Read task files from `tasks/todo/`
- Use `tasks/reference/` when relevant
- Treat each task as isolated

## Blocked Behavior
If the task cannot be completed safely, produce a clear explanation for `tasks/blocked/`
```

---

## Prompt Context Block for Tasks

If needed, include this at the top of each task file:

```markdown
## Context

This task is being executed by an automated overnight runner.

- Changes will be committed automatically after this task completes
- Do not ask clarifying questions
- Make reasonable assumptions and keep them minimal
- Follow all conventions in `CLAUDE.md`
- If the task cannot be completed safely, create a blocked explanation
- All relevant tests should pass before considering the task complete
```

If this guidance is already in `CLAUDE.md`, it does not need to be repeated in every task file.

---

## Morning Review Workflow

```bash
git pull
git log --oneline
git show <commit-hash>
git revert <commit-hash>
```

Typical workflow:
1. Pull overnight changes
2. Review commit history
3. Inspect each task commit independently
4. Keep, revert, or revise as needed
5. Queue the next batch of tasks

---

## AI-Optimized Summary

### Project Type
- Windows automation tool
- .NET console application
- local repository orchestration
- Git-integrated coding workflow
- unattended AI task execution

### Key Invariants
- one completed task = one commit
- task files are processed in deterministic filename order
- repository settings override global settings
- blocked tasks must be explicit
- human review happens after execution, before merge
- no automatic merge to main
- no reliance on cross-task memory

### Primary Inputs
- `appsettings.json`
- `tasks/settings.json`
- `CLAUDE.md`
- `tasks/todo/*.md`
- repository working tree
- optional `tasks/reference/*`

### Primary Outputs
- code changes in repository
- Git commits
- moved task files
- logs
- blocked explanations
- pushed branch updates if configured

### Preferred AI Behavior
- be deterministic
- stay in scope
- preserve conventions
- avoid unnecessary changes
- fail clearly
- keep commits atomic
