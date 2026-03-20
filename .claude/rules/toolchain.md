# Toolchain

Three tools enforce code style in this order of authority:

## 1. CSharpier — Final Formatting Authority
CSharpier is the final word on formatting. If CSharpier reformats something, that is correct.

Configuration (`.csharpierrc.json`):
```json
{
  "printWidth": 128,
  "useTabs": true,
  "tabWidth": 4
}
```

Run before committing. Do not manually format what CSharpier will reformat.

## 2. ReSharper / Rider — Profile: CineMassive_Default
Run code cleanup after every change using the `CineMassive_Default` ReSharper profile. It enforces:
- Removes unused `using` statements
- Enforces `var` usage
- Makes fields `readonly` where possible
- Removes redundant code

## 3. jb cleanupcode — Post-Task Cleanup

After completing all C# file writes for a task, run `jb cleanupcode` to apply the `CineMassive_Default` profile across every file you created or modified.

**Discovering the solution file:**
1. Look for a `.sln` file in the same directory as `task.md` (the repo root)
2. If exactly one `.sln` exists, use it automatically
3. If more than one exists, check `CLAUDE.md` for a `Solution:` directive specifying which to use

**Command format:**
```powershell
jb cleanupcode .\SolutionName.sln --profile="CineMassive_Default" --include="Path/To/File1.cs;Path/To/File2.cs"
```

- Paths in `--include` are relative to the solution file location
- Separate multiple files with `;`
- Run once at the end of the task, not after each individual file write
- This step is mandatory — do not skip it

## 4. .editorconfig — Naming Conventions
Naming rules are enforced as warnings via `.editorconfig`. Violations show up in the IDE.
Namespace must match folder structure exactly — `.editorconfig` enforces this.

## Expression-Bodied Members
Expression-bodied methods and properties are banned. Always use block bodies.

```csharp
// Correct
public string GetName()
{
    return name;
}

// Banned
public string GetName() => name;
```
