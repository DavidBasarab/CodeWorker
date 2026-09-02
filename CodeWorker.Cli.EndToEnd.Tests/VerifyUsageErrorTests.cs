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
