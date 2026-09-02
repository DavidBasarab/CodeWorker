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

		var result = await RunValidInvocation(workspace);

		result.StandardOutput.Should().Contain("verify: parsed intent");
	}

	[Fact]
	public async Task IncludeTheIntentPathInTheParsedOutput()
	{
		using var workspace = new TempWorkspace();

		var result = await RunValidInvocation(workspace);

		result.StandardOutput.Should().Contain(workspace.IntentPath);
	}

	[Fact]
	public async Task ExitZeroForAValidInvocation()
	{
		using var workspace = new TempWorkspace();

		var result = await RunValidInvocation(workspace);

		result.ExitCode.Should().Be(0);
	}

	private async Task<CliResult> RunValidInvocation(TempWorkspace workspace)
	{
		return await runner.Run(
			publishedCli.ExecutablePath,
			"verify",
			"--intent",
			workspace.IntentPath,
			"--production",
			workspace.ProductionPath,
			"--tests",
			workspace.TestsPath
		);
	}
}
