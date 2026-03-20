# Naming & Structure

## Core Philosophy
- Follow Clean Code principles (Robert C. Martin) and SOLID.
- Methods do one thing. Classes have one responsibility.
- Code reads like prose. Names make intent obvious without reading the implementation.
- Prefer interfaces and polymorphism over if/switch chains.
- Do NOT over-engineer. Do NOT introduce abstractions that do not already exist in this codebase.
- Match the abstraction level and style of the surrounding code.

## Naming Rules
- Avoid abbreviations. Prefer full words so readers never have to guess meaning.
- Acceptable abbreviations: widely recognized acronyms (e.g. `HTTP`, `URL`, `ID`) and any abbreviation that appears among the top 3 Google results for that term. When in doubt, use the full word.
- Names reveal intent. A method name makes it unnecessary to read the body.
- No comments explaining what code does — rename until it is obvious.
- PascalCase: classes, interfaces, methods, properties, constants
- camelCase: local variables, parameters, private fields — no leading underscore
- Private fields prefer `readonly` for dependencies where applicable
- Boolean names read as questions or states: `isReady`, `hasOutputs`, `canRestore`
- String interpolation required — never string concatenation with `+`

## Method Size
- Methods should be as short as possible.
- ~10 lines is a signal to evaluate refactoring — not an automatic rule.
- No method should require a comment to explain what it does. Refactor or rename instead.

## Spacing
- Leave a blank line between method definitions.
- Leave a blank line after variable declarations in a method before logic begins.
- Leave a blank line before return statements.

## Control Flow
- Avoid deep if/else nesting. Prefer guard clauses and early returns to keep the main flow readable.
- Avoid complex nested ternary expressions — prefer clear `if` statements or extract into a well-named method.
- If you need to explain what code does with a comment, first ask whether a better name makes the comment unnecessary.

## Files & Namespaces
- One class per file. File named after the class, never the interface.
- Exception: if a class directly implements a single interface, both may be in the same file — still named after the class.
- Namespace must exactly match the folder path within the project. No exceptions.
- Test project mirrors source project: same folder structure, same namespace with `Tests.` prepended.

## Interfaces
- All interfaces use the `I` prefix.
- Interface names describe a capability or action: `IClearDatabase`, `IRestoreBackup`.
- NOT: `IDatabase`, `IBackupService` — these describe what something is, not what it does.
- Default to narrow, single-purpose interfaces. One interface = one capability.
- Exception: highly cohesive groups (e.g. all REST calls to the same API resource) may be grouped.
- All cross-boundary dependencies must be interfaces: threading, file system, time, external processes, REST clients.
- If something cannot be faked in a test, it is not properly abstracted.
