# Tasks

This folder is managed by [CodeWorker](https://github.com/DavidBasarab/CodeWorker) — an overnight task runner for Claude Code.

## Folder Structure

| Folder | Purpose |
|--------|---------|
| `todo/` | Place task files here. They are executed in filename order. |
| `done/` | Completed tasks are moved here automatically. |
| `blocked/` | Tasks that could not be completed are moved here with an explanation. |

## Task File Format

Task files are plain Markdown. The filename prefix controls execution order:

```
01-refactor-auth-service.md
02-add-unit-tests-auth.md
03-update-api-docs.md
```

Each file contains a self-contained prompt describing exactly what Claude should do.
