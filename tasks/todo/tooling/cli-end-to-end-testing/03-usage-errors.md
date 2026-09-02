# Phase 3 — Usage-Error E2E (missing flags, missing files, no arguments)

- **Work item:** cli-end-to-end-testing (see
  `tasks/todo/tooling/cli-end-to-end-testing/00-overview.md`)
- **Depends on:** Phase 1 (`01-harness-spine.md`); **uses** `TempWorkspace` from Phase 2
- **Depended on by:** —
- **Risk:** **Low** — stdout assertions on the Phase 1 harness. Test-only.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Read Phase 1's **Hand-off** (harness types) and Phase 2's **Hand-off** (`TempWorkspace`).

**The exact strings under test** come from `ParseVerifyArguments`; `VerifyCommand` logs them as
`verify: {Reason}` at `Error` level → they land on stdout (synchronous console sink). Assert with
`.Contain(...)` on the stable fragment.

**Critical: the parser's short-circuit order** (verified against `ParseVerifyArguments.Parse`) — every
**flag-presence** check runs **before** any **file-existence** check:

1. no flag present at all → `NoArguments` → `"Usage: verify --intent <intent.json> --production <Foo.cs> --tests <FooTests.cs>"`
2. `--intent` absent (but some flag present) → `"Missing required flag --intent. …"`
3. `--production` absent → `"Missing required flag --production. …"`
4. `--tests` absent → `"Missing required flag --tests. …"`
5. intent file missing → `"Intent file not found: <path>. …"`
6. production file missing → `"Production file not found: <path>. …"`
7. tests file missing → `"Tests file not found: <path>. …"`

Consequences for arg construction:

- **Missing-flag cases (2–4)** short-circuit *before* any existence check, so the two flags you *do*
  pass need only placeholder values — the files need not exist. To hit case 3 you must supply `--intent`
  and `--tests` (so their presence checks pass) and omit `--production`; likewise for 2 and 4.
- **Missing-file cases (5–7)** require **all three flags present** and the *earlier* files to **exist**
  (so the parser reaches the one you want to fail). Use `TempWorkspace` paths for the files that must
  exist and a guaranteed-missing path for the one under test.
- **No-arguments (1)** is triggered by passing **no flags** — either the bare exe or just `verify`.
  (Phase 1 already asserts the banner on the bare exe; here assert the `Usage:` line.)

## Design (build exactly this shape)

### `CodeWorker.Cli.EndToEnd.Tests/VerifyUsageErrorTests.cs`

A private `Run(params string[] args)` helper keeps each `[Fact]` to a single assertion. A
guaranteed-missing path is any path not written to disk.

```csharp
using Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection(EndToEndCollection.Name)]
public class VerifyUsageErrorTests(PublishedCli publishedCli)
{
	private readonly CliProcessRunner runner = new();

	[Fact]
	public async Task ReportUsageWhenNoArgumentsProvided()
	{
		var result = await Run("verify");

		result.StandardOutput.Should().Contain("Usage: verify --intent");
	}

	[Fact]
	public async Task ReportMissingIntentFlag()
	{
		var result = await Run("verify", "--production", "p", "--tests", "t");

		result.StandardOutput.Should().Contain("Missing required flag --intent");
	}

	[Fact]
	public async Task ReportMissingProductionFlag()
	{
		var result = await Run("verify", "--intent", "i", "--tests", "t");

		result.StandardOutput.Should().Contain("Missing required flag --production");
	}

	[Fact]
	public async Task ReportMissingTestsFlag()
	{
		var result = await Run("verify", "--intent", "i", "--production", "p");

		result.StandardOutput.Should().Contain("Missing required flag --tests");
	}

	[Fact]
	public async Task ReportIntentFileNotFound()
	{
		using var workspace = new TempWorkspace();

		var missing = MissingPath();

		var result = await Run(
			"verify",
			"--intent",
			missing,
			"--production",
			workspace.ProductionPath,
			"--tests",
			workspace.TestsPath
		);

		result.StandardOutput.Should().Contain("Intent file not found:");
	}

	[Fact]
	public async Task ReportProductionFileNotFound()
	{
		using var workspace = new TempWorkspace();

		var missing = MissingPath();

		var result = await Run(
			"verify",
			"--intent",
			workspace.IntentPath,
			"--production",
			missing,
			"--tests",
			workspace.TestsPath
		);

		result.StandardOutput.Should().Contain("Production file not found:");
	}

	[Fact]
	public async Task ReportTestsFileNotFound()
	{
		using var workspace = new TempWorkspace();

		var missing = MissingPath();

		var result = await Run(
			"verify",
			"--intent",
			workspace.IntentPath,
			"--production",
			workspace.ProductionPath,
			"--tests",
			missing
		);

		result.StandardOutput.Should().Contain("Tests file not found:");
	}

	[Fact]
	public async Task ExitZeroOnAUsageError()
	{
		var result = await Run("verify", "--production", "p", "--tests", "t");

		result.ExitCode.Should().Be(0);
	}

	private async Task<CliResult> Run(params string[] args)
	{
		return await runner.Run(publishedCli.ExecutablePath, args);
	}

	private static string MissingPath()
	{
		return Path.Combine(Path.GetTempPath(), $"codeworker-cli-e2e-missing-{Guid.NewGuid():N}");
	}
}
```

`ExitZeroOnAUsageError` is the deliberate tripwire for ADR-3: today a usage error still exits 0. When
the **Verdict model + reporting** item wires a non-zero exit, this test (and the assertion) is updated
as part of that item — the failure is the signal, by design.

> Note on `runner.Run(publishedCli.ExecutablePath, args)`: `CliProcessRunner.Run` takes
> `(string executablePath, params string[] arguments)`, so forwarding an existing `args` array is a
> normal call — no spread needed.

An xUnit `[Theory]` + `[MemberData]` over `(args, expectedFragment)` is an acceptable equivalent if
`code-review` prefers it; keep one logical assertion per case either way.

## Steps (TDD — tests first, red before green)

1. Write `VerifyUsageErrorTests` and watch each case fail first if you stub the expectations wrong —
   then confirm green against the real published exe:
   `dotnet test CodeWorker.sln --filter "Category=EndToEnd"`.
2. Cross-check one message against the source (`ParseVerifyArguments`) so the asserted fragment is a
   real substring of the rendered line, prefix and all.

## Definition of Done (all mandatory)

- [ ] Tests written first (red before green — verify a wrong expected fragment fails, then correct it)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln --filter "Category=EndToEnd"` — all E2E tests pass (Phases 1–3)
- [ ] `dotnet test CodeWorker.sln --filter "Category!=EndToEnd"` — unit suites still pass, no publish triggered
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespaces match folder paths; one class per file; verb-first one-assertion tests; no banned patterns
- [ ] Review loop until all three pass clean, restarting from the top after any fix: `unit-test-review`
      (must end `Unit test review: PASS`) → `code-review` → `code-security-review`
- [ ] Exactly one commit on the current branch (`CliTester`), message referencing this file; **no push**

Suggested commit message:

```
cli-end-to-end-testing phase 3: usage-error verify E2E (tasks/todo/tooling/cli-end-to-end-testing/03-usage-errors.md)
```

## Rollback Procedure

- `git revert <this commit>`. Removes `VerifyUsageErrorTests`; Phases 1–2 remain green. No data/config steps.

## Phase Report (produce before finishing)

Files added/changed; test counts (new/total/passing under the E2E filter); deviation log (empty log is
a claim, not a default) — especially any message fragment that differed from the source and had to be
adjusted, and confirmation that every usage-error case exits 0. Open questions/risks for the reviewer.

## Hand-off

- **This phase exposes no new types.** It completes the E2E suite: banner smoke (Phase 1), green path
  (Phase 2), and every usage-error branch (Phase 3), all pinned to today's exit-0 contract.
- **Behavior note for the next Foundation item:** when a real gate (compile/test/mutation) or the
  verdict/exit-code wiring lands, extend this suite with its E2E case and revisit the exit-0 assertions
  here — they are intentionally the tripwire.
