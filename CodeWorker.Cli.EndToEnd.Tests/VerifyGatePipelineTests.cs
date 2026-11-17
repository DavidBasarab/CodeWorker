using Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection(EndToEndCollection.Name)]
public class VerifyGatePipelineTests(PublishedCli publishedCli)
{
	private const string MalformedIntent = "{ not json";

	private readonly CliProcessRunner runner = new();

	[Fact]
	public async Task ReportThePipelineSummaryForAValidInvocation()
	{
		using var workspace = new TempWorkspace();

		var result = await RunVerify(workspace);

		result.StandardOutput.Should().Contain("verify: gate pipeline finished with 0 gates");
	}

	[Fact]
	public async Task ReportTheEmptyPipelineAsNotBlocked()
	{
		using var workspace = new TempWorkspace();

		var result = await RunVerify(workspace);

		result.StandardOutput.Should().Contain("blocked: False");
	}

	[Fact]
	public async Task NotRunThePipelineForAnInvalidIntent()
	{
		using var workspace = new TempWorkspace();

		workspace.WriteIntent(MalformedIntent);

		var result = await RunVerify(workspace);

		result.StandardOutput.Should().Not.Contain("verify: gate pipeline");
	}

	[Fact]
	public async Task ExitZeroWhenThePipelineFinishes()
	{
		using var workspace = new TempWorkspace();

		var result = await RunVerify(workspace);

		result.ExitCode.Should().Be(0);
	}

	private async Task<CliResult> RunVerify(TempWorkspace workspace)
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
