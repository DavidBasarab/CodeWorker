# Phase 2 — Green-Path E2E (valid `verify` invocation)

- **Work item:** cli-end-to-end-testing (see
  `tasks/todo/tooling/cli-end-to-end-testing/00-overview.md`)
- **Depends on:** Phase 1 (`01-harness-spine.md`)
- **Depended on by:** —
- **Risk:** **Low** — one test built entirely on the Phase 1 harness plus a small temp-file helper.
  Test-only.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Then read Phase 1's **Hand-off** section: you consume `PublishedCli`, `CliProcessRunner`,
`CliResult`, and `EndToEndCollection` exactly as defined there.

**What the green path proves:** given `verify --intent <i> --production <p> --tests <t>` where all
three files **exist on disk**, the CLI parses them and prints, via the Serilog console sink →
stdout, the exact log the code emits:

```
verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}
```

(from `VerifyCommand.Execute` → the `IsValid` branch). The parser reaches this branch only when every
flag is present **and** `IFileSystemTools.FileExists` returns true for each path — so the test must put
real files on disk. The parser scans the whole args array, so passing the leading `verify` verb is
correct and realistic.

The rendered Serilog line is prefixed with a timestamp + level and renders the message template with
the values substituted (no braces in the output). Assert with `.Contain(...)` on the fragment
`verify: parsed intent` and on each of the three paths — never exact-line equality.

## Design (build exactly this shape)

### `CodeWorker.Cli.EndToEnd.Tests/Harness/TempWorkspace.cs`

A tiny `IDisposable` that creates a temp directory with the three files the green path needs and hands
back their absolute paths. Content is irrelevant to today's surface — only existence is checked — so a
minimal but plausible payload is fine. `[ExcludeFromCodeCoverage]` (a thin `System.IO` wrapper, ADR-4):

```csharp
using System.Diagnostics.CodeAnalysis;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[ExcludeFromCodeCoverage(
	Justification = "Thin System.IO temp-file wrapper — no business logic, exercised by the E2E tests that use it."
)]
public class TempWorkspace : IDisposable
{
	private readonly string root;

	public TempWorkspace()
	{
		root = Path.Combine(Path.GetTempPath(), $"codeworker-cli-e2e-work-{Guid.NewGuid():N}");

		Directory.CreateDirectory(root);

		IntentPath = WriteFile("intent.json", "{ \"class\": \"Foo\" }");
		ProductionPath = WriteFile("Foo.cs", "public class Foo { }");
		TestsPath = WriteFile("FooTests.cs", "public class FooTests { }");
	}

	public string IntentPath { get; }

	public string ProductionPath { get; }

	public string TestsPath { get; }

	public void Dispose()
	{
		try
		{
			Directory.Delete(root, true);
		}
		catch
		{
			// ignored — best-effort cleanup of a temp directory
		}
	}

	private string WriteFile(string name, string content)
	{
		var path = Path.Combine(root, name);

		File.WriteAllText(path, content);

		return path;
	}
}
```

### `CodeWorker.Cli.EndToEnd.Tests/VerifyValidInvocationTests.cs`

```csharp
using Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection(EndToEndCollection.Name)]
public class VerifyValidInvocationTests(PublishedCli publishedCli)
{
	private readonly CliProcessRunner runner = new();

	[Fact]
	public async Task ReportTheParsedPathsForAValidInvocation()
	{
		using var workspace = new TempWorkspace();

		var result = await runner.Run(
			publishedCli.ExecutablePath,
			"verify",
			"--intent",
			workspace.IntentPath,
			"--production",
			workspace.ProductionPath,
			"--tests",
			workspace.TestsPath
		);

		result.StandardOutput.Should().Contain("verify: parsed intent");
	}

	[Fact]
	public async Task IncludeTheIntentPathInTheParsedOutput()
	{
		using var workspace = new TempWorkspace();

		var result = await runner.Run(
			publishedCli.ExecutablePath,
			"verify",
			"--intent",
			workspace.IntentPath,
			"--production",
			workspace.ProductionPath,
			"--tests",
			workspace.TestsPath
		);

		result.StandardOutput.Should().Contain(workspace.IntentPath);
	}

	[Fact]
	public async Task ExitZeroForAValidInvocation()
	{
		using var workspace = new TempWorkspace();

		var result = await runner.Run(
			publishedCli.ExecutablePath,
			"verify",
			"--intent",
			workspace.IntentPath,
			"--production",
			workspace.ProductionPath,
			"--tests",
			workspace.TestsPath
		);

		result.ExitCode.Should().Be(0);
	}
}
```

One assertion per test (`testing.md`). If the three near-identical setups read as noise to
`code-review`, a private helper that runs the valid invocation and returns the `CliResult` is an
acceptable refactor — keep each `[Fact]` to its single assertion.

## Steps (TDD — tests first, red before green)

1. Write `VerifyValidInvocationTests` **first**, referencing `TempWorkspace` (not yet created) — red
   (compile failure, then a failing run).
2. Add `TempWorkspace`. Run `dotnet test CodeWorker.sln --filter "Category=EndToEnd"` — the green-path
   tests pass alongside Phase 1's smoke tests.
3. Sanity: the printed path in the output matches the temp path you passed (proves the real parse ran,
   not a canned string).

## Definition of Done (all mandatory)

- [ ] Tests written before `TempWorkspace` (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln --filter "Category=EndToEnd"` — all E2E tests pass (Phase 1 + Phase 2)
- [ ] `dotnet test CodeWorker.sln --filter "Category!=EndToEnd"` — unit suites still pass, no publish triggered
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; `[ExcludeFromCodeCoverage]` justification on
      `TempWorkspace`; verb-first one-assertion tests; no banned patterns
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review`
      (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current branch (`CliTester`), message referencing this file; **no push**

Suggested commit message:

```
cli-end-to-end-testing phase 2: green-path verify E2E (tasks/todo/tooling/cli-end-to-end-testing/02-valid-invocation.md)
```

## Rollback Procedure

- `git revert <this commit>`. Removes `TempWorkspace` and `VerifyValidInvocationTests`; the Phase 1
  harness and smoke test remain green. No data/config steps.

## Phase Report (produce before finishing)

Files added/changed; test counts (new/total/passing under the E2E filter); deviation log (empty log is
a claim, not a default) — especially any adjustment to the asserted message fragment if the real
rendered Serilog line differs from `verify: parsed intent …`. Open questions/risks for the reviewer.

## Hand-off

- **Types this phase exposes to later phases** (namespace `Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness`):
  - `TempWorkspace : IDisposable` — creates a temp dir with `intent.json`, `Foo.cs`, `FooTests.cs`;
    exposes `IntentPath`, `ProductionPath`, `TestsPath`. Phase 3 uses it for the missing-file cases
    (point a flag at a path inside the workspace that was never written).
- **Behavior notes:** the green branch requires **all three files to exist**; omit or mispoint any one
  and the parser takes an error branch (Phase 3's territory).
