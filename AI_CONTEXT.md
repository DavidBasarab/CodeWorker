# AI_CONTEXT.md

## Purpose
Windows-based overnight automation runner for local repositories. It executes queued coding tasks using Claude Code, commits each completed task separately, and prepares results for human review the next morning.

## Core Model
- Human writes task files during the day
- Windows Scheduled Task starts runner overnight
- Runner processes repositories sequentially
- Runner processes task files in deterministic filename order
- Claude executes each task
- Each successful task creates exactly one Git commit
- Completed tasks move to `tasks/done/`
- Blocked tasks move to `tasks/blocked/`
- Results are pushed for morning review

## Key Invariants
- One task = one logical unit of work
- One completed task = one commit
- Tasks are self-contained
- No cross-task memory should be assumed
- Human review is always required before merge
- No automatic merge to main
- Fail visibly, not silently
- Prefer minimal, scoped, reviewable changes

## Repository Structure
```text
/your-project
  /tasks
    /todo
    /done
    /blocked
    /pending
    /reference
    settings.json
  /src
  CLAUDE.md
```

## Folder Meanings
- `tasks/todo/` = queued tasks
- `tasks/done/` = completed tasks
- `tasks/blocked/` = blocked tasks with explanation
- `tasks/pending/` = deferred tasks
- `tasks/reference/` = optional supporting context
- `CLAUDE.md` = repository-level AI instructions
- `tasks/settings.json` = repository-level overrides

## Runner Workflow
For each enabled repository:
1. Load global config
2. Load repository config overrides
3. Validate repository and task folders
4. Pull latest Git changes
5. Discover `tasks/todo/*.md`
6. Sort tasks by filename ascending
7. Run one task at a time
8. Execute Claude with task content plus repository instructions
9. Determine success, blocked, pending, or failure
10. Commit if successful and configured
11. Move task file to appropriate folder
12. Push if configured

## AI Execution Contract
The AI:
- may read repository files
- may read `CLAUDE.md`
- may read current task file
- may use `tasks/reference/` when relevant
- should not assume reliable memory between tasks

The AI should:
- stay within scope
- preserve conventions
- avoid unrelated refactors
- avoid changing public APIs unless requested
- prefer correctness over speed
- make minimal necessary assumptions
- keep changes atomic and reviewable
- consider tests/build when relevant
- document blockers clearly

The AI should not:
- invent product requirements
- perform broad opportunistic refactors
- change unrelated files
- silently ignore blockers
- leave ambiguous partial implementations

## Task Requirements
A task should be:
- specific
- bounded
- testable
- self-contained
- explicit about scope and constraints

Recommended task sections:
- Objective
- Scope
- Requirements
- Constraints
- Acceptance Criteria
- Notes

## Task State Model
States:
- Todo
- Running
- Done
- Blocked
- Pending
- Failed

Transitions:
```text
Todo -> Running -> Done
Todo -> Running -> Blocked
Todo -> Running -> Pending
Todo -> Running -> Failed
```

## Blocked Task Rule
If a task cannot be completed safely:
- do not silently guess
- create a blocked explanation
- move task to `tasks/blocked/`
- include reason, evidence, and recommended next step

## Commit Strategy
- Exactly one commit per successful task
- Commit only after task completion
- Commit message should map clearly to task filename

Recommended format:
```text
🤖 01-refactor-auth-service
```

## Rollback Model
Because each task is one commit:
- revert a single task with `git revert <commit>`
- revert later tasks together if needed
- revise and re-queue prompts for dependent fixes rather than patching blindly

## Config Model
Global config: `appsettings.json`
Repository overrides: `tasks/settings.json`

Repository settings override global settings property-by-property.

Key settings categories:
- `Repositories`
- `Git`
- `Claude`
- `Tasks`
- `Notifications`

## Quality Rules
A successful task should leave the repository in a valid state:
- requested work completed
- no unresolved blocker
- relevant tests/build pass when required
- commit created if configured
- task moved to correct folder

## Logging
Recommended events:
- runner start/end
- repository start/end
- task discovered/start/end
- Claude start/end
- validation result
- commit created
- push completed
- blocker detected
- task moved

Recommended fields:
- timestamp
- repository
- task filename
- state
- elapsed time
- commit hash
- error details

## Suggested CLAUDE.md Rules
- Stay within task scope
- Make the smallest reasonable change
- Preserve repository conventions
- Avoid unrelated refactors
- Do not ask clarifying questions during unattended execution
- If blocked, explain clearly
- Build/tests should pass when relevant
- Do not leave partial implementations silently

## Optional Task Context Block
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

## Human Review Model
Morning workflow:
```bash
git pull
git log --oneline
git show <commit-hash>
git revert <commit-hash>
```

Human reviews all results before merge or release.
