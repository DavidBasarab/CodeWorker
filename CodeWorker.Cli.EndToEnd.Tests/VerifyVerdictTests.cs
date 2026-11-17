using System.Text.Json;
using Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection(EndToEndCollection.Name)]
public class VerifyVerdictTests(PublishedCli publishedCli)
{
	private const string MalformedIntent = "{ not json";

	private readonly CliProcessRunner runner = new();

	[Fact]
	public async Task WriteAGreenVerdictForAValidInvocation()
	{
		using var workspace = new TempWorkspace();

		await RunVerify(workspace);

		ReadVerdictStatus(workspace).Should().Be("green");
	}

	[Fact]
	public async Task WriteAnInvalidIntentVerdict()
	{
		using var workspace = new TempWorkspace();

		workspace.WriteIntent(MalformedIntent);

		await RunVerify(workspace);

		ReadVerdictStatus(workspace).Should().Be("invalidIntent");
	}

	[Fact]
	public async Task ExitTwoForAnInvalidIntentWithAVerdict()
	{
		using var workspace = new TempWorkspace();

		workspace.WriteIntent(MalformedIntent);

		var result = await RunVerify(workspace);

		result.ExitCode.Should().Be(2);
	}

	[Fact]
	public async Task WriteNoVerdictFileOnAUsageError()
	{
		using var workspace = new TempWorkspace();

		await runner.Run(
			publishedCli.ExecutablePath,
			"verify",
			"--intent",
			workspace.IntentPath,
			"--production",
			workspace.ProductionPath,
			"--verdict",
			workspace.VerdictPath
		);

		File.Exists(workspace.VerdictPath).Should().BeFalse();
	}

	[Fact]
	public async Task PrintTheVerdictReport()
	{
		using var workspace = new TempWorkspace();

		var result = await RunVerify(workspace);

		result.StandardOutput.Should().Contain("Verdict: GREEN");
	}

	private static string ReadVerdictStatus(TempWorkspace workspace)
	{
		using var verdict = JsonDocument.Parse(File.ReadAllText(workspace.VerdictPath));

		return verdict.RootElement.GetProperty("status").GetString();
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
			workspace.TestsPath,
			"--verdict",
			workspace.VerdictPath
		);
	}
}
