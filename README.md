# Claude Overnight Task Runner

## Vision

A lightweight Windows-based automation system that lets a developer queue up coding tasks during the day and have Claude Code execute them overnight — unattended. Each morning, the developer reviews clean, committed changes and queues the next batch.

The goal is to act as a **personal overnight engineering assistant**: you define the work, Claude does it, Git preserves the history, and you stay in control of what lands.

---

## How It Works

A Windows Scheduled Task fires at a configured time (e.g. 2:00 AM). It runs a .NET console application that:

1. Reads a configuration file listing one or more local repository paths to process
2. For each repository, performs a `git pull` to get the latest code
3. Looks in the `tasks/todo/` folder at the root of the repo for task files
4. Processes each task file **in order** (alphabetical/numbered filename ordering)
5. Shells out to `claude -p` with the contents of each task file
6. After each task completes, commits the changes with a descriptive message
7. Moves the completed task file to `tasks/done/`
8. Pushes the commits so results are available for review in the morning

---

## Repository Structure

Each monitored repository follows this convention:

```
/your-project
  /tasks
    /todo
      01-refactor-auth-service.md
      02-add-unit-tests-auth.md
      03-update-api-docs.md
    /done
      00-initial-setup.md        ← completed tasks move here
  /src
    ...
  CLAUDE.md                      ← project-level instructions for Claude
```

Task files are plain Markdown. The filename prefix controls execution order. Each file contains a self-contained prompt describing exactly what Claude should do.

---

## Task File Format

```markdown
# Refactor Auth Service

Refactor the authentication service in `/src/services/AuthService.cs` to use
the middleware pattern. Specifically:

- Extract token validation into `AuthMiddleware.cs`
- Remove duplicate validation logic in `UserController.cs`
- Ensure all existing unit tests still pass
- Follow the patterns established in `PaymentMiddleware.cs`

Do not change the public interface of `AuthService`.
```

---

## Git Commit Strategy

Each task produces exactly **one commit**. This is intentional and critical to the rollback workflow.

```
commit a1b2c3  ←  01-refactor-auth-service    (Task 1)
commit d4e5f6  ←  02-add-unit-tests-auth       (Task 2)
commit g7h8i9  ←  03-update-api-docs           (Task 3)
```

You review the diff for each commit independently the next morning. If you dislike a change, you have clean, predictable rollback options.

---

## Rollback Scenarios

### Roll back everything from Task 2 onward

Tasks 2 and 3 built on top of Task 1 — you want to discard all of it:

```bash
git reset --hard a1b2c3
# or, to keep history intact:
git revert d4e5f6 g7h8i9
```

### Roll back only Task 2, keep Task 3

Harder, because Task 3 may depend on Task 2's changes. Attempt:

```bash
git revert d4e5f6   # reverts Task 2
# Resolve any conflicts, then re-queue Task 2 with a revised prompt
```

### Cleanest approach for dependent tasks

Write Task 2 with a revised prompt, place it back in `tasks/todo/`, and let the runner re-execute it the following night. Tasks 3 and beyond pick up from the corrected base automatically.

---

## Configuration

The application reads a config file (`appsettings.json`) at startup.  These are global settings that can be overriden at the repository level.

```json
{
  "Repositories": [
    {
      "Path": "C:\\Projects\\my-api",
      "Enabled": true,
      "SettingsPath" : "C:\\Projects\\my-api\\tasks\\settings.json"
    },
    {
      "Path": "C:\\Projects\\my-frontend",
      "SettingsPath" : "C:\\Projects\\my-api\\tasks\\settings.json",
      "Enabled": true
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
  },
}
```

Multiple repositories are supported. Each is processed sequentially in the order listed.  Will not move on to the next repository in all the tasks are complete in the previous repository.

## Repository Configuration

These basic settings will be added to `tasks\settings.json` when setting up the repository.  This will override global settings in order to support being more remote in the development.

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
    "ReferenceFolder" : "tasks/reference",
    "PendingFolder" : "tasks/pending",
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

## Windows Task Scheduler Setup

```powershell
$action = New-ScheduledTaskAction `
  -Execute "C:\Tools\ClaudeRunner\ClaudeRunner.exe"

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

Set `WakeToRun` if your machine sleeps at night. The 6-hour execution limit prevents runaway sessions.

---

## Including This Vision in Prompts

Every task file should begin with a brief system context block so Claude understands the environment it is operating in. Paste this at the top of each task file, or reference it from your repo's `CLAUDE.md`:

```markdown
## Context

This task is being executed by an automated overnight runner.

- Changes will be committed automatically after this task completes
- Do not ask clarifying questions — make reasonable assumptions and note them in the commit message
- Follow all conventions in CLAUDE.md
- If a task cannot be completed safely, create a file at `tasks/blocked/<filename>.md` explaining why
- All tests should pass before considering a task complete
```

Placing this context in `CLAUDE.md` at the repo root means Claude picks it up automatically on every run without needing to repeat it in each task file.

---

## Morning Review Workflow

```bash
# Pull overnight results
git pull

# Review what ran
git log --oneline

# Inspect a specific task's changes
git show d4e5f6

# If you like everything, queue the next batch
# Drop new .md files into tasks/todo/ and push

# If you want to undo a task
git revert <commit-hash>
```

---

## Design Principles

**One task, one commit.** Granular commits make every change reviewable and independently reversible. Never batch multiple tasks into a single commit.

**Tasks are self-contained.** Each task file should describe enough context to execute without relying on the runner's session state. Claude has no memory between tasks.

**Fail visibly.** If Claude cannot complete a task, it should write a blocked file rather than produce broken code silently. The runner moves the task to `tasks/blocked/` and continues to the next task.

**You stay in control.** Nothing merges to your main branch automatically. The runner commits to whatever branch is currently checked out. You review and decide what stays.

**Prompts are code.** Task files live in the repo alongside the code they affect. They are version-controlled, can be iterated on, and serve as a record of intent — not just a log of what changed, but why.