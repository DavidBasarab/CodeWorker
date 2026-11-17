# Phase 4 — JSON Verdict Serializer

- **Work item:** verdict-model-reporting (see `tasks/todo/foundation/verdict-model-reporting/00-overview.md`)
- **Depends on:** Phase 1
- **Depended on by:** Phase 6
- **Wave:** 2
- **Parallel with:** Phase 5 — both depend only on Phase 1; disjoint write sets; no shared hotspot
- **Risk:** **High** (auto: public contract) — this JSON is what the `Stop` hook parses.

## Touches

- Creates: `CodeWorker.Cli/Commands/Verify/Verdict/SerializeVerdict.cs`,
  `CodeWorker.Cli.Tests/Commands/Verify/Verdict/SerializeVerdictTests.cs`
- Modifies: nothing
- Exclusive runtime resources: none

Phase 5 runs at the same time in its own worktree. Create or modify **only** these files.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first — mandatory.

**Worktree base:** before reading code, run `git merge --ff-only <wave base>` (the commit the
orchestrator names) in your still-clean worktree, then confirm `git rev-parse HEAD` equals it.

Built by Phase 1 (namespace `FatCat.CodeWorker.Cli.Commands.Verify.Verdict`):
`Verdict { VerdictStatus Status; string Message; List<GateVerdict> Gates }`,
`GateVerdict { string Name; GateStage Stage; GateStatus Status; long DurationMilliseconds; List<GateFinding> Findings }`,
`VerdictStatus { Green, Blocked, InvalidIntent }`. `GateFinding { string FilePath; int? Line; string Reason }`
and the `GateStage` / `GateStatus` enums are in `…Commands.Verify.Gates`.

House JSON pattern: `System.Text.Json` with a `private static readonly JsonSerializerOptions` field
(see `Commands/Verify/Intent/ParseIntent.cs`).

## Design (build exactly this shape)

`SerializeVerdict.cs` — interface + class:
```csharp
public interface ISerializeVerdict
{
	string Serialize(Verdict verdict);
}

public class SerializeVerdict : ISerializeVerdict
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
	};

	public string Serialize(Verdict verdict) { ... JsonSerializer.Serialize(verdict, JsonOptions) ... }
}
```
Target shape (overview ADR-5):
```json
{ "status": "blocked", "message": "…", "gates": [ { "name": "Compile", "stage": "compile",
  "status": "failed", "durationMilliseconds": 120, "findings": [ { "filePath": "Foo.cs", "line": 12, "reason": "…" } ] } ] }
```
`InvalidIntent` serializes as `"invalidIntent"`.

## Steps (TDD — tests first, red before green)

`SerializeVerdictTests` (namespace `Testing.FatCat.CodeWorker.Cli.Commands.Verify.Verdict`), SUT in the
constructor. Assert by parsing the output with `JsonDocument.Parse` and reading one property per test
(not by comparing whole strings — whitespace is not the contract). One assertion each:

1. `WriteTheStatusAsACamelCaseString` — Blocked → `"blocked"`.
2. `WriteInvalidIntentAsCamelCase` — `"invalidIntent"`.
3. `WriteTheMessage`
4. `WriteEachGateName`
5. `WriteTheGateStageAsACamelCaseString` — `Compile` → `"compile"`.
6. `WriteTheGateStatusAsACamelCaseString` — `Failed` → `"failed"`.
7. `WriteTheDurationInMilliseconds` — `"durationMilliseconds"` number.
8. `WriteTheFindingFilePathLineAndReason` — (three tests if needed, one assertion each)
9. `OmitANullFindingLine` — `"line"` property absent.
10. `UseCamelCasePropertyNames` — root has `"gates"`, not `"Gates"`.

Red first, then implement to green.

## Definition of Done (all mandatory)

- [ ] Tests written before implementation (red observed before green)
- [ ] `dotnet build CodeWorker.slnx` — zero warnings
- [ ] `dotnet test CodeWorker.slnx` — all tests pass
- [ ] `dotnet format style CodeWorker.slnx` and `dotnet format analyzers CodeWorker.slnx` run; build again so CSharpier applies
- [ ] Namespaces match folder paths; no banned patterns
- [ ] Only the files in `## Touches` created/modified
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review` (screen-only — write nothing outside `## Touches`)
- [ ] Exactly one commit on your worktree branch, message referencing this file; no push

Suggested commit message:

```
verdict-model-reporting phase 4: JSON verdict serializer (tasks/todo/foundation/verdict-model-reporting/04-verdict-json.md)
```

## Rollback Procedure

- Revert Phase 6 first if committed, then `git revert <this commit>`. No manual steps.

## Phase Report (produce before finishing)

Files added/changed/deleted; test counts; deviation log (an empty log is a claim, not a default);
open questions/risks.

## Hand-off

- `ISerializeVerdict.Serialize(Verdict) → string` — the JSON contract the `Stop` hook reads (pinned
  field names: `status`, `message`, `gates[].name|stage|status|durationMilliseconds|findings[].filePath|line|reason`).
