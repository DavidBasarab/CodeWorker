# Haivision Coding Standards

This file defines the coding standards for this codebase (Command 360).
All code you generate — in any context — must follow the rules for the relevant language below.
The goal is that AI-generated code is indistinguishable from code written by a senior member of this team.

---

## C# Rules

Apply these rules to all C# code. Do not apply them to React, TypeScript, PowerShell, or any other language.

@.claude/rules/csharp/naming-and-structure.md
@.claude/rules/csharp/types-and-di.md
@.claude/rules/csharp/toolchain.md
@.claude/rules/csharp/async.md
@.claude/rules/csharp/errors-and-logging.md
@.claude/rules/csharp/testing.md
@.claude/rules/csharp/not-allowed.md

## PowerShell Rules

Apply these rules to all PowerShell scripts. Do not apply them to C#, React, TypeScript, or any other language.

@.claude/rules/powershell/powershell.md

## TypeScript & React Rules

Apply these rules to all TypeScript and TSX files — both the React frontend (Sites/main) and the Node.js CLI tools (Installer/DataMigration). Do not apply them to C#, PowerShell, or any other language.

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
