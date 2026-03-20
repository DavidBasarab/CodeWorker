# PowerShell Standards

## Runtime

- Always use `pwsh` (PowerShell 7). Never use `powershell.exe` (PowerShell 5).
- Always load the user profile when invoking `pwsh` via the Bash tool: prefix every `-Command` with `. $PROFILE; `.
- Single quotes are required around the `-Command` argument so bash does not expand `$PROFILE` before pwsh sees it.
  ```
  pwsh -Command '. $PROFILE; Your-Command'
  ```

---

## Scope
These standards apply to all PowerShell scripts in this codebase. PowerShell is used for:
- Build automation
- Windows environment configuration
- Application API interaction and deployment automation

---

## Naming

- All functions use `Verb-Noun` format with an approved PowerShell verb from `Get-Verb`.
- PascalCase for everything: function names, parameters, variables, hashtable keys.
- Full words only — no abbreviations unless they meet the top-3 Google rule (same as C#).
- No aliases in code. Always use full cmdlet names (`Get-ChildItem` not `gci`, `Where-Object` not `where`).
- Users may define their own aliases in their profile — do not define aliases for them in scripts.

---

## Files

- One function per file. The file is named after the function: `Deploy-MyApp.ps1` contains only `Deploy-MyApp`.
- Exception: helper functions defined inside another function's scope (nested functions) may live in the same file as their parent.
- Do not define multiple top-level functions in a single file.

---

## Function Structure

- Every function has a `param()` block at the top.
- Parameters are typed explicitly.
- Use `[switch]` for boolean flags — never a `[bool]` parameter with `$true/$false`.
- Use `[Parameter(Mandatory = $true)]` only when the parameter is genuinely required to function.
- Do not use `#Requires` statements.

```powershell
function Deploy-Something {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Server,
        [int]$BuildNumber,
        [switch]$SkipInitialize
    )

    # function body
}
```

---

## Error Handling

- `$ErrorActionPreference = "Stop"` is used in build scripts to halt on any error.
- Do not set `$ErrorActionPreference = "Stop"` globally in API interaction or environment scripts — let callers decide.
- Use `-ErrorAction SilentlyContinue` on specific commands where failure is acceptable (e.g. `Remove-Item` cleanup).

---

## Output & Logging

- Build scripts use a dedicated logging function for all output — not `Write-Host`.
- Interactive and API scripts use `Write-Host` with `-ForegroundColor` for user-facing output.
- Suppress unwanted pipeline output with `| Out-Null` — not `> $null`.

---

## REST API Calls

- Use `Invoke-RestMethod` for all REST API interactions.
- Use `Invoke-WebRequest` only when `Invoke-RestMethod` cannot handle the response (rare).
- Always pass `-SkipCertificateCheck` for internal API calls.
- Always pass the `Authorization` header with a Bearer token.

```powershell
Invoke-RestMethod "https://$Server/api/resource" `
    -Method GET `
    -Headers @{ Authorization = "Bearer $accessToken" } `
    -SkipCertificateCheck
```

---

## Code Style

- Use backtick (`` ` ``) line continuation for long calls — one parameter per line, consistently indented.
- Use splatting (`@params`) for very long parameter lists when backtick continuation becomes unwieldy.
- Use `Push-Location` / `Pop-Location` to preserve the working directory when a script changes location.
- Use `Join-Path` for all path construction — never string concatenation for paths.
- Use `Test-Path` before creating or removing files/directories.
- Suppress directory creation output with `| Out-Null`.

```powershell
if (-not (Test-Path $outputPath)) {
    New-Item -ItemType Directory -Path $outputPath | Out-Null
}
```

---

## Comments

- Use comments sparingly. Prefer a well-named function over a comment explaining what code does.
- Comment only when the intent behind a decision is not obvious from reading the code.
- A comment explaining *why* something is done is acceptable. A comment explaining *what* is not.

---

## What NOT to Do

- Do NOT use aliases (`gci`, `where`, `%`, `?`, etc.) — always use full cmdlet names
- Do NOT define aliases for users
- Do NOT use `[bool]` parameters — use `[switch]`
- Do NOT use `> $null` to suppress output — use `| Out-Null`
- Do NOT use `Invoke-WebRequest` when `Invoke-RestMethod` will work
- Do NOT use string concatenation for paths — use `Join-Path`
- Do NOT use `#Requires` statements
- Do NOT set `$ErrorActionPreference = "Stop"` outside of build scripts
