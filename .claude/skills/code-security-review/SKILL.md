---
name: code-security-review
description: Full-application security review. Always scans the ENTIRE source tree — never just a diff, project, or commit — and flags the top vulnerability classes using OWASP (Top 10, ASVS), Microsoft Secure Coding Guidance, the CWE Top 25, and GitHub CodeQL query coverage as the knowledge base. Use when the user says "security review", "check for vulnerabilities", "scan for security issues", or similar. Writes a dated, branch-stamped report to vulnerabilities/unresolved/ at the repo root — unless screen-only output is requested (e.g. by another Claude instance), in which case the report is printed in the session and no file is written. When a session later fixes a report's findings, the report moves to vulnerabilities/resolved/ — that move happens in the fixing session, never in this skill. This skill NEVER edits source code.
---

# Code Security Review

Review the **entire application** for security vulnerabilities and produce an actionable report. This skill **only reads and reports** — it does not fix code, does not commit anything, does not delete reports, and does not move reports between folders.

Unlike a style-focused code review, security review is never diff-scoped: a vulnerability can live in code nobody touched this week, and a change in one file can make old code in another file exploitable. **Every run covers the full source tree.** If the user names a narrower target ("security review CodeWorker.Cli"), still review everything — you may organize the report so their named area appears first, but do not skip the rest.

## Report folders

Reports live in two committed folders at the repo root:

- `vulnerabilities/unresolved/` — reports whose findings have not all been fixed yet. New reports always land here.
- `vulnerabilities/resolved/` — reports whose findings have all been fixed. Reports are moved here **only by the session that fixed them** (see "Resolution workflow" below), never by this skill.

Create either folder if it does not exist.

## Step 0 — Choose the output mode

Two output modes:

- **File mode (default):** write the report to `vulnerabilities/unresolved/`.
- **Screen mode:** print the full report as markdown in the session and write **nothing** to disk.

Use screen mode when any of the following is true:
- The skill is invoked with an argument such as `screen`, `no-file`, `no-write`, or `stdout` (e.g. `/code-security-review screen`).
- The user's phrasing asks for it: "don't write a file", "just show me", "output to the screen", "inline".
- The skill is being run by another Claude instance / automated session as part of a larger task, where the findings will be consumed directly from the conversation rather than from disk.

If none of those apply, use file mode.

## Step 1 — Gather the scope and run context

The scope is always the whole repository, from the repo root (`C:\Code\CodeWorker`):

- Review source and configuration that can carry vulnerabilities: `*.cs`, `*.ps1`, `*.csproj` (package references), `appsettings*.json` and any settings JSON, `*.yml`/`*.yaml`, `Dockerfile`, compose files.
- Skip generated code, `bin/`, `obj/`, `node_modules/`, lock files, and prior reports in `vulnerabilities/`.
- Test projects (`CodeWorker.Tests`, `CodeWorker.Cli.Tests`) are in scope **only** for hard-coded secrets and credentials — application vulnerability classes do not apply to code that does not ship.
- Review the **current working-tree content** of each file (uncommitted changes included — they are part of the application as it stands).

Record the run context up front — it goes in the report header:
- **Datetime:** `pwsh -Command '. $PROFILE; (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")'` (and `yyyy-MM-dd-HHmmss` for the filename).
- **Branch:** `git branch --show-current` (if empty — detached HEAD — use `git rev-parse --short HEAD`).
- **Commit:** `git rev-parse --short HEAD`.

## Step 2 — Know what to look for

The knowledge base for this review is the union of these four sources. Do not invent exotic findings — anchor every finding to at least one of them:

1. **OWASP** — Top 10 (A01 Broken Access Control … A10 SSRF) and the relevant ASVS controls.
2. **Microsoft Secure Coding Guidance** — .NET-specific guidance: secure process invocation, secure use of cryptography APIs, `System.Random` vs `RandomNumberGenerator`, deserialization (`BinaryFormatter` is banned), path handling, secrets in configuration.
3. **CWE Top 25** — the current Top 25 Most Dangerous Software Weaknesses. Tag every finding with its CWE ID (e.g. CWE-78, CWE-22, CWE-798, CWE-502, CWE-88).
4. **GitHub CodeQL** — the classes of issues CodeQL's C# query packs flag: tainted data flows (source → sink), command-line injection, log forging, regex injection, weak randomness, hard-coded credentials, XML external entities.

### High-signal checks for this codebase

CodeWorker is a .NET 10 CLI tool that runs Claude against markdown task files in tracked repositories. Its attack surface is not a web request boundary — it is **the external processes it launches, the files it reads and writes, and the fact that it may run unattended on a schedule with the invoking user's privileges.** Check these deliberately, not just generically:

- **Command / argument injection (CWE-78/CWE-88):** CodeWorker invokes external processes — the Claude CLI, `git`, and PowerShell. Any process command line, argument, or shell string built from task-file content, settings values, repository paths, or other externally-controlled input is a finding. Prefer argument arrays over a single shell string; never pass untrusted data through `cmd /c`, `bash -c`, or `Invoke-Expression`. Flag `powershell.exe` where `pwsh` is required, and any PowerShell invocation that concatenates untrusted strings into the script text.
- **Path traversal (CWE-22):** file reads and writes whose paths derive from task markdown, settings JSON, or command arguments — anything that could escape the intended tracked-repo working directory (`..\`, absolute paths, symlinks). CodeWorker writes into real repositories; a traversal here can corrupt or overwrite files outside the target repo. Treat writes as **higher severity** than reads.
- **Untrusted task markdown and settings (CWE-20, A08):** task files are instructions that drive an automated agent. Content that can steer CodeWorker into destructive or unintended actions (deleting files, force operations, exfiltration) is a finding — validate and constrain what task-driven flows are permitted to do, especially destructive git operations. Settings JSON parsed into typed models must not enable polymorphic/`TypeNameHandling` deserialization (see below).
- **Unattended / scheduled execution:** CodeWorker is designed to run overnight without a human present. Destructive operations reachable from task or settings input — `git reset --hard`, `git clean -fdx`, force push, bulk file deletion, `Remove-Item -Recurse -Force` — must be gated and must not act on paths derived from untrusted input. An unattended destructive action with no confirmation path is a finding.
- **Deserialization (CWE-502):** `BinaryFormatter`, `Newtonsoft` with `TypeNameHandling`, or any polymorphic deserialization of settings/task JSON or other untrusted data.
- **Secrets (CWE-798):** API keys or tokens for Claude/Anthropic, git credentials or PATs, or any token hard-coded in source or committed in `appsettings*.json` / settings JSON. Name the key and redact the value in the report.
- **Sensitive data in logs/output (CWE-532):** secrets, tokens, credentials, or full contents of a user's private repo files reaching logs, exception messages, or console output. Log at the action site with identifiers, not raw secret material.
- **Weak randomness (CWE-338):** `System.Random` (or a generator backed by it) used for anything with security meaning — tokens, temporary file names in shared locations. Use `RandomNumberGenerator` where the value must be unpredictable.
- **Insecure crypto (CWE-327):** MD5/SHA1 or a home-rolled scheme used for a security purpose; hard-coded keys or IVs.
- **SSRF (A10, CWE-918):** any outbound request whose URL is influenced by task or settings input.
- **Transport/config:** Docker images running as root with secrets baked in; credentials or tokens passed on a process command line where they are visible in the process table.
- **Vulnerable dependencies (A06):** check `dotnet list package --vulnerable` when feasible and flag known-vulnerable references.

## Step 3 — Review each file

Go file by file, tracing externally-controlled data from where it enters (task markdown files, settings JSON, command-line arguments, environment variables) to where it is used (process command lines and arguments, file/directory paths, PowerShell script text, logs, outbound HTTP, crypto operations).

For every finding capture:
- **File and line (or range).**
- **Severity:** `Critical` (remotely or unattended-exploitable, direct compromise — arbitrary command execution, destructive action on untrusted input, credential theft), `High` (exploitable with conditions, or leakage of secrets/private repo contents), `Medium` (defense-in-depth gap, hardening), `Low` (best-practice deviation with limited impact).
- **Classification:** CWE ID + OWASP category (and CodeQL query family or Microsoft guidance reference where it applies).
- **Problem:** what an attacker (or a malicious/compromised task file) can actually do, in plain language.
- **Suggested fix:** the concrete change — named APIs, patterns, or code direction that complies with the repo's coding standards (e.g. fixes must still use constructor injection, `IThread` for threading, enum-return for known failure modes rather than thrown exceptions, `pwsh` not `powershell.exe`).

Before writing a finding, check `vulnerabilities/unresolved/` for existing reports: if the same issue is already reported there, still include it (each report stands alone as a full picture of the branch at its run time), but note that it also appears in the earlier report.

Be precise and honest. Do not pad the report with theoretical findings that have no path to exploitation here — if something is only an observation, label it as one. If the codebase is clean, say so.

## Step 4 — Report

Order findings by severity (Critical first). Use the template below in both modes.

**Screen mode:** print the full report in the session. State clearly that no file was written.

**File mode:**
- Ensure `vulnerabilities/unresolved/` exists.
- Filename: `vulnerabilities/unresolved/<YYYY-MM-DD-HHmmss>-<branch-slug>.md` — branch slug is the branch name with `/` replaced by `-`.
- Write the report even when there are **no findings** — a dated clean pass on a branch is itself a useful record. A clean report may be written directly to `vulnerabilities/resolved/` since there is nothing to resolve.
- After writing, tell the user the report path and a one-line summary (counts by severity).
- Do not commit the report or anything else — committing is the user's decision. The folders are intentionally **not** gitignored, so reports can be committed with the code they describe.

### Report template

```markdown
# Security Review — full application

- **Run:** <yyyy-MM-dd HH:mm:ss>
- **Branch:** <branch name>
- **Commit:** <short hash of HEAD>
- **Reviewed:** entire source tree (<N> files)
- **Result:** <N> finding(s) — <c> Critical, <h> High, <m> Medium, <l> Low
- **Sources:** OWASP Top 10 / ASVS, Microsoft Secure Coding Guidance, CWE Top 25, CodeQL query coverage
- **Status:** UNRESOLVED

> Generated by `/code-security-review`. This skill never edits source code itself.
>
> **To the session asked to fix this report:** fix the findings below in the source code,
> run `dotnet build CodeWorker.sln` and `dotnet test CodeWorker.sln` to confirm green, and check
> off each finding in the summary checklist as you resolve it. When — and only when — every
> finding is fixed and verified, change **Status** above to `RESOLVED (<date>)` and move this
> file to `vulnerabilities/resolved/` (same filename). If any finding remains unfixed, the
> file stays in `vulnerabilities/unresolved/` with the checklist showing what is left.

---

## 1. <short title> — <Severity>
- **File:** <relative/path/to/File.cs>, lines <range>
- **Classification:** CWE-<id> — <name>; OWASP <category>
- **Problem:** <what an attacker or malicious task file can do and why this code allows it>
- **Suggested fix:** <the concrete change>

## 2. <next finding>
...

---

## Observations (not vulnerabilities)
- <hardening suggestions or notes that are not exploitable findings — omit section if none>

## Summary checklist
- [ ] 1. <File.cs> — <one-line of what to fix> (<Severity>)
```

## Resolution workflow (for the fixing session — not this skill)

When the user says "fix this security report", "run a fix on <report>", or points a session at a file in `vulnerabilities/unresolved/`:

1. Fix each finding in the source code, following the repo's coding standards (TDD included — security fixes get tests like any other production change).
2. Run `dotnet build CodeWorker.sln` and `dotnet test CodeWorker.sln`; everything must be green.
3. Check off each resolved finding in the report's summary checklist.
4. **All findings fixed:** update the report's **Status** line to `RESOLVED (<yyyy-MM-dd>)` and move the file to `vulnerabilities/resolved/`, keeping the same filename (use `git mv` if the report is tracked).
5. **Some findings remain:** leave the file in `vulnerabilities/unresolved/` with the checklist reflecting what is done and what is left. Never move a partially resolved report.

This move is the **only** sanctioned way a report leaves `unresolved/` — the review skill itself never moves, renames, or deletes reports.

## Hard rules for this skill
- **Always review the entire source tree** — never narrow to a diff, commit, project, or directory, even if asked; the full application is the point.
- **Never edit source files.** Review and report only; fixing is a separate, explicit action.
- **Never move or delete reports.** New reports go to `unresolved/`; moving to `resolved/` belongs exclusively to the session that fixed the findings.
- **Do not commit anything.**
- Every finding must trace to OWASP, Microsoft Secure Coding Guidance, the CWE Top 25, or a CodeQL query class. Anything else goes under Observations.
- The report itself must not leak sensitive material: reference files, lines, and identifiers — never reproduce secret values (name the key, redact the value), and do not paste the raw contents of a user's private repository files or task content beyond the minimal snippet needed to locate the finding.
