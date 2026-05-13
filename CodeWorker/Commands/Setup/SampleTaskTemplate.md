# Sample Task Template

> **This is a template.** Copy it into `tasks/todo/`, rename it with the next number, and fill it in. Do not run it as-is.

## Why Planning Matters

CodeWorker executes tasks **unattended overnight**. There is no human in the loop between "task lands in `todo/`" and "commit lands in git the next morning." That makes the task file itself the entire contract with an AI that has no memory of prior tasks and cannot ask clarifying questions.

A precise, well-bounded task that took 20 minutes to plan produces a clean reviewable commit. A vague task written in 30 seconds produces noise that takes longer to triage in the morning than it would have taken to plan correctly. **Planning is the work, not overhead.**

## Naming Convention

Rename this file to `NN-short-description.md` where `NN` is one higher than the highest number currently in `tasks/done/` (and any other queued task in `tasks/todo/`). Filename order is execution order. Move it into `tasks/todo/` to queue it.

## Objective

State, in one or two sentences, what the task accomplishes and why it matters. Lead with the outcome, not the steps.

## Scope

### Files to add

- `path/to/NewFile.cs` — what it contains and why.

### Files to change

- `path/to/ExistingFile.cs` — the change being made.

### Out of scope

- Anything you considered and explicitly excluded. Name it so the runner does not drift into it.

## Requirements

- Numbered, testable requirements. Each one should be verifiable by reading code or running a test.
- Include any non-obvious constraints (interfaces to implement, patterns to follow, registrations to update).

## Constraints

- Reference the relevant rule files in `.claude/rules/` that must be followed.
- Call out any abstraction, dependency, or pattern that must be reused rather than reinvented.
- Name files, types, methods, and folders the runner should NOT touch.

## Acceptance Criteria

- Concrete, observable outcomes that prove the task is done.
- Tests passing, build clean, formatter clean — name the commands.
- Manual smoke-test steps if the change is user-visible.

## Blocked Conditions

The runner is pre-authorized to stop and write a `tasks/blocked/` explanation file (instead of completing the task) when any of these occur:

- A required file referenced in **Scope** does not exist.
- Baseline tests are already failing before any change is made.
- Instructions in this file contradict each other or contradict the rules in `.claude/rules/`.
- A required dependency, package, or tool is missing from the environment.
- The runner cannot determine the next numeric prefix safely.

When blocked, do not guess. Move the task to `tasks/blocked/` with a sibling explanation file describing exactly which condition triggered the block.

## Notes

- Anything the runner needs to know that does not fit elsewhere: prior decisions, links to related tasks, pointers to reference material in `tasks/reference/`.
- Keep notes terse. If a note is load-bearing for correctness, promote it into **Requirements** or **Constraints**.
