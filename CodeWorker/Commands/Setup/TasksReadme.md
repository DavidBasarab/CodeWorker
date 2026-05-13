# Tasks

This folder is managed by [CodeWorker](https://github.com/DavidBasarab/CodeWorker) — an overnight task runner for Claude Code.

## Why Planning Matters

CodeWorker executes tasks **unattended overnight**. There is no human in the loop between "task lands in `todo/`" and "commit lands in git the next morning." That makes the task file itself the entire contract with an AI that has no memory of prior tasks and cannot ask clarifying questions.

A precise, well-bounded task that took 20 minutes to plan produces a clean reviewable commit. A vague task written in 30 seconds produces noise that takes longer to triage in the morning than it would have taken to plan correctly. **Planning is the work, not overhead.**

## Folder Structure

| Folder | Purpose |
|--------|---------|
| `todo/` | Queue of task files waiting to run. Executed in filename order. |
| `pending/` | Task currently being processed by the runner. |
| `done/` | Completed tasks. |
| `blocked/` | Tasks the runner could not safely complete. Each has a sibling explanation file. |
| `failed/` | Tasks that errored out during execution. |
| `reference/` | Supporting context the runner can read while executing tasks. |
| `logs/` | Per-task log output (`<task>.log` and `<task>.live.log`). |

## Naming Convention

Task files use a zero-padded numeric prefix that is **one higher than the highest number currently in `tasks/done/`** (and any other queued task in `tasks/todo/`). Filename order is execution order:

```
01-refactor-auth-service.md
02-add-unit-tests-auth.md
03-update-api-docs.md
```

## Sample Template

A starter template lives next to this file at [`sample-task-template.md`](sample-task-template.md). Copy it into `tasks/todo/`, rename it with the next number, and fill it in.

## Where to Learn More

See the **Task File Requirements** section of the [CodeWorker README](https://github.com/DavidBasarab/CodeWorker#task-file-requirements) for the full contract.
