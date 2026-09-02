# Phase 1 — E2E Project + Publish/Run Harness (the spine)

- **Work item:** cli-end-to-end-testing (see
  `tasks/todo/tooling/cli-end-to-end-testing/00-overview.md`)
- **Depends on:** —
- **Depended on by:** Phase 2 (`02-valid-invocation.md`), Phase 3 (`03-usage-errors.md`)
- **Risk:** **Medium** — introduces a new project + solution wiring, a `dotnet publish` fixture, and
  `System.Diagnostics.Process` invocation with stream capture. These are the flaky-prone parts (exe
  path resolution, stdout/stderr deadlock, temp cleanup). Test-only: no production code changes, no
  auth/anonymous/data-migration/public-API surface, so not auto-high.

## Context (complete handoff — read before coding)

Read `CodeWorker.Cli/README.md`, `CLAUDE.md`, and **all** `.claude/rules/csharp/*.md` first —
mandatory. Pay special attention to `testing.md` (xUnit, FluentAssertions, verb-first one-assertion
tests, block bodies, the `[ExcludeFromCodeCoverage]` low-level-wrapper exemption) and
`naming-and-structure.md` (interface names describe a capability; one class per file; no over-engineering).

**This phase builds the harness only** — the project, the publish fixture, the process runner, and a
single smoke test that proves publish → invoke → capture works end to end. Phases 2 and 3 write the
real verify assertions on top of it.

Current state you will find (verified against the code):

- Four projects in `CodeWorker.sln` (legacy format): `CodeWorker`, `CodeWorker.Tests`,
  `CodeWorker.Cli`, `CodeWorker.Cli.Tests`. You add a **fifth**: `CodeWorker.Cli.EndToEnd.Tests`.
- `CodeWorker.Cli.csproj` → `OutputType=Exe`, `net10.0`, `AssemblyName=FatCatCodeWorkerCli`. Publishing
  it yields **`FatCatCodeWorkerCli.exe`** on Windows.
- `CodeWorkerCliApplication.Run` logs `"Welcome to Code Worker CLI"` (Information) on **every**
  invocation, via the synchronous Serilog console sink registered in `CodeWorkerCliModule`. That banner
  is the smoke test's assertion target — it proves the real exe booted and logged to stdout.
- `CodeWorker.Cli.Tests.csproj` is the template for packages/props: `net10.0`, `ImplicitUsings=enable`,
  `Nullable=disable`, `OutputType=Library`, `NoWarn=$(NoWarn);NETSDK1206`, and package refs
  `Microsoft.NET.Test.Sdk` 18.3.0, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.5, `FluentAssertions`,
  `FatCat.Fakes`, `CSharpier.MsBuild` 1.2.6. **Drop FakeItEasy** (nothing is faked in a black-box test);
  **do not** add a `ProjectReference` to the CLI (ADR-2 — black box).

## Design (build exactly this shape)

New project folder `CodeWorker.Cli.EndToEnd.Tests/`. Namespaces mirror folders under
`Testing.FatCat.CodeWorker.Cli.EndToEnd`.

### `CodeWorker.Cli.EndToEnd.Tests/CodeWorker.Cli.EndToEnd.Tests.csproj`

Mirror `CodeWorker.Cli.Tests.csproj`, with `RootNamespace`/`AssemblyName`
`Testing.FatCat.CodeWorker.Cli.EndToEnd`, no FakeItEasy, and **no `ProjectReference`**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net10.0</TargetFramework>
		<ImplicitUsings>enable</ImplicitUsings>
		<Nullable>disable</Nullable>
		<RootNamespace>Testing.FatCat.CodeWorker.Cli.EndToEnd</RootNamespace>
		<AssemblyName>Testing.FatCat.CodeWorker.Cli.EndToEnd</AssemblyName>
		<LangVersion>default</LangVersion>
		<OutputType>Library</OutputType>
	</PropertyGroup>
	<PropertyGroup>
		<NoWarn>$(NoWarn);NETSDK1206</NoWarn>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="FluentAssertions" Version="*" />
		<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.3.0" />
		<PackageReference Include="xunit" Version="2.9.3" />
		<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
		<PackageReference Include="CSharpier.MsBuild" Version="1.2.6">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
		</PackageReference>
	</ItemGroup>
</Project>
```

### Add the project to `CodeWorker.sln`

Add a `Project(...)` block with a **fresh, unique GUID** and the four config rows the other projects
have (`Debug|Any CPU`, `Release|Any CPU`, and the `x64`/`x86` → `Any CPU` maps — copy the shape of an
existing project's rows exactly). Generate a GUID with
`pwsh -Command '. $PROFILE; [guid]::NewGuid().ToString().ToUpper()'`. Verify with
`dotnet sln CodeWorker.sln list` and a solution build.

### `CodeWorker.Cli.EndToEnd.Tests/GlobalUsings.cs`

```csharp
global using System.Threading.Tasks;
global using FluentAssertions;
global using Xunit;
```

### `CodeWorker.Cli.EndToEnd.Tests/Harness/CliResult.cs`

Plain value carrying what a caller observes. Namespace
`Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness`:

```csharp
namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

public class CliResult
{
	public int ExitCode { get; set; }

	public string StandardOutput { get; set; }

	public string StandardError { get; set; }
}
```

### `CodeWorker.Cli.EndToEnd.Tests/Harness/CliProcessRunner.cs`

Wraps `System.Diagnostics.Process`. **`[ExcludeFromCodeCoverage]`** (ADR-4). Reads both streams to end
**before** `WaitForExit` to avoid the classic full-pipe deadlock:

```csharp
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over System.Diagnostics.Process — no business logic, exercised by the E2E tests that drive it."
)]
public class CliProcessRunner
{
	public async Task<CliResult> Run(string executablePath, params string[] arguments)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = executablePath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		using var process = Process.Start(startInfo);

		var standardOutput = process.StandardOutput.ReadToEndAsync();
		var standardError = process.StandardError.ReadToEndAsync();

		await process.WaitForExitAsync();

		return new CliResult
		{
			ExitCode = process.ExitCode,
			StandardOutput = await standardOutput,
			StandardError = await standardError,
		};
	}
}
```

Notes: `ArgumentList` (not a single `Arguments` string) so paths with spaces are passed safely — no
manual quoting. Start the two `ReadToEndAsync` reads before awaiting exit.

### `CodeWorker.Cli.EndToEnd.Tests/Harness/PublishedCli.cs`

Publishes the CLI **once** and exposes the exe path; deletes the temp dir on dispose. Implements
xUnit's `IAsyncLifetime` so the publish runs once for the whole collection.
**`[ExcludeFromCodeCoverage]`** (ADR-4):

```csharp
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[ExcludeFromCodeCoverage(
	Justification = "Publishes the CLI via the dotnet CLI (System.Diagnostics.Process) — no business logic, exercised by the E2E tests."
)]
public class PublishedCli : IAsyncLifetime
{
	private string publishDirectory;

	public string ExecutablePath { get; private set; }

	public async Task InitializeAsync()
	{
		publishDirectory = Path.Combine(Path.GetTempPath(), $"codeworker-cli-e2e-{Guid.NewGuid():N}");

		var projectPath = Path.Combine(LocateRepositoryRoot(), "CodeWorker.Cli", "CodeWorker.Cli.csproj");

		await Publish(projectPath, publishDirectory);

		ExecutablePath = Path.Combine(publishDirectory, "FatCatCodeWorkerCli.exe");

		if (!File.Exists(ExecutablePath))
		{
			throw new FileNotFoundException($"Published CLI not found at {ExecutablePath}");
		}
	}

	public Task DisposeAsync()
	{
		try
		{
			Directory.Delete(publishDirectory, true);
		}
		catch
		{
			// ignored — best-effort cleanup of a temp directory
		}

		return Task.CompletedTask;
	}

	private static string LocateRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "CodeWorker.sln")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate CodeWorker.sln above the test output directory.");
	}

	private static async Task Publish(string projectPath, string outputDirectory)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		startInfo.ArgumentList.Add("publish");
		startInfo.ArgumentList.Add(projectPath);
		startInfo.ArgumentList.Add("-c");
		startInfo.ArgumentList.Add("Release");
		startInfo.ArgumentList.Add("-o");
		startInfo.ArgumentList.Add(outputDirectory);

		using var process = Process.Start(startInfo);

		var output = process.StandardOutput.ReadToEndAsync();
		var error = process.StandardError.ReadToEndAsync();

		await process.WaitForExitAsync();

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(
				$"dotnet publish failed (exit {process.ExitCode}).{Environment.NewLine}{await output}{Environment.NewLine}{await error}"
			);
		}
	}
}
```

### `CodeWorker.Cli.EndToEnd.Tests/Harness/EndToEndCollection.cs`

One collection so the single `PublishedCli` instance is shared across every E2E test class (publish
happens once):

```csharp
namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[CollectionDefinition(Name)]
public class EndToEndCollection : ICollectionFixture<PublishedCli>
{
	public const string Name = "CodeWorker CLI end-to-end";
}
```

### `CodeWorker.Cli.EndToEnd.Tests/VerifyBannerTests.cs` (the smoke test)

Proves the whole harness works: publish, run the real exe with no args, capture stdout, see the banner.
Class-level `[Trait("Category", "EndToEnd")]` (applies to every method) and the collection binding:

```csharp
using Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection(EndToEndCollection.Name)]
public class VerifyBannerTests(PublishedCli publishedCli)
{
	private readonly CliProcessRunner runner = new();

	[Fact]
	public async Task PrintTheWelcomeBanner()
	{
		var result = await runner.Run(publishedCli.ExecutablePath);

		result.StandardOutput.Should().Contain("Welcome to Code Worker CLI");
	}

	[Fact]
	public async Task ExitZeroWithNoArguments()
	{
		var result = await runner.Run(publishedCli.ExecutablePath);

		result.ExitCode.Should().Be(0);
	}
}
```

## Steps (TDD — tests first, red before green)

1. Create the project, add it to `CodeWorker.sln`, add `GlobalUsings.cs`. Write `VerifyBannerTests`
   **first** — it will not compile / will fail because the harness types don't exist yet (red).
2. Add `CliResult`, `CliProcessRunner`, `PublishedCli`, `EndToEndCollection` to make it green.
3. Run **only** the E2E suite: `dotnet test CodeWorker.sln --filter "Category=EndToEnd"` — both smoke
   tests pass (publish succeeds, banner appears, exit 0).
4. Confirm the fast loop **excludes** them: `dotnet test CodeWorker.sln --filter "Category!=EndToEnd"`
   runs the existing unit suites and does **not** trigger a publish.
5. Manually eyeball a publish once if useful:
   `dotnet publish CodeWorker.Cli/CodeWorker.Cli.csproj -c Release -o <temp>` then run
   `<temp>/FatCatCodeWorkerCli.exe` — the banner prints.

## Definition of Done (all mandatory)

- [ ] Smoke tests written before the harness (red observed before green)
- [ ] `dotnet build CodeWorker.sln` — zero warnings
- [ ] `dotnet test CodeWorker.sln --filter "Category=EndToEnd"` — E2E tests pass
- [ ] `dotnet test CodeWorker.sln --filter "Category!=EndToEnd"` — existing unit suites pass, no publish triggered
- [ ] `dotnet format style CodeWorker.sln` and `dotnet format analyzers CodeWorker.sln` run; then
      `dotnet build` again so CSharpier applies
- [ ] Namespaces match folder paths exactly; one class per file; interface names describe capability;
      `[ExcludeFromCodeCoverage]` with a specific justification on both process/publish wrappers; no
      banned patterns (no expression-bodied members, no `async void`, no records, collection expressions
      where applicable)
- [ ] Review loop until all three pass clean, in order, restarting from the top after any fix:
      `unit-test-review` (must end `Unit test review: PASS`) → `code-review` → `code-security-review`.
      Note for `unit-test-review`: the harness classes are the low-level-wrapper exemption (ADR-4); the
      E2E test methods are their coverage — no faked unit tests are expected for them.
- [ ] Exactly one commit on the current branch (`CliTester`), message referencing this file; **no push**

Suggested commit message:

```
cli-end-to-end-testing phase 1: E2E project + publish/run harness (tasks/todo/tooling/cli-end-to-end-testing/01-harness-spine.md)
```

## Rollback Procedure

- If Phase 2 or Phase 3 exist, revert them first (they depend on this phase). Then
  `git revert <this commit>`.
- No data/config/feature-flag steps. The revert removes the new project and its `.sln` entry; verify
  `dotnet build CodeWorker.sln` is green afterward.

## Phase Report (produce before finishing)

Files added/changed/deleted (including the `.sln` edit); test counts (new/total/passing) for both the
E2E filter and the unit filter; deviation log (every departure from this plan and why — an empty log is
a claim, not a default). Note the measured one-time publish cost and the resolved exe path. Open
questions/risks for the reviewer (e.g. any publish-configuration or exe-name surprise).

## Hand-off

- **Types this phase exposes to later phases** (namespace `Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness`):
  - `CliResult` — `{ int ExitCode; string StandardOutput; string StandardError; }`.
  - `CliProcessRunner` — `Task<CliResult> Run(string executablePath, params string[] arguments)`;
    captures stdout/stderr/exit without deadlock.
  - `PublishedCli` — collection fixture (`IAsyncLifetime`); `string ExecutablePath` is the published
    `FatCatCodeWorkerCli.exe`. Publishes once per run, cleans up on dispose.
  - `EndToEndCollection` — `[CollectionDefinition]`; later test classes use
    `[Collection(EndToEndCollection.Name)]` + constructor-inject `PublishedCli`.
- **Behavior notes for later phases:**
  - Tag every E2E test class `[Trait("Category", "EndToEnd")]` so it stays in the filtered set.
  - Assert with `.Contain(...)`, never exact-line equality (Serilog prefixes timestamp + level).
  - The published exe **always** exits 0 today (ADR-3) — pin `ExitCode.Should().Be(0)`.
  - To create files the CLI must see as existing, write real temp files (the parser calls
    `IFileSystemTools.FileExists`) — see Phase 2.
