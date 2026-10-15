using Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd;

[Trait("Category", "EndToEnd")]
[Collection(EndToEndCollection.Name)]
public class VerifyIntentTests(PublishedCli publishedCli)
{
	private const string MalformedIntent = "{ not json";

	private readonly CliProcessRunner runner = new();

	[Fact]
	public async Task ReportTheIntentClassForAValidIntent()
	{
		using var workspace = new TempWorkspace();

		var result = await RunVerify(workspace);

		result.StandardOutput.Should().Contain("verify: intent for");
	}

	[Fact]
	public async Task ReportMalformedIntentJson()
	{
		using var workspace = new TempWorkspace();

		workspace.WriteIntent(MalformedIntent);

		var result = await RunVerify(workspace);

		result.StandardOutput.Should().Contain("Intent file is not a valid intent JSON object");
	}

	[Fact]
	public async Task ReportANonObservableBehavior()
	{
		using var workspace = new TempWorkspace();

		workspace.WriteIntent(IntentFor("Foo", "Uses a switch"));

		var result = await RunVerify(workspace);

		result.StandardOutput.Should().Contain("is not observable");
	}

	[Fact]
	public async Task ReportAClassThatDoesNotMatchTheProductionFile()
	{
		using var workspace = new TempWorkspace();

		workspace.WriteIntent(IntentFor("Bar", "Returns nothing when constructed"));

		var result = await RunVerify(workspace);

		result.StandardOutput.Should().Contain("does not match the production file");
	}

	[Fact]
	public async Task ExitZeroForAnInvalidIntent()
	{
		using var workspace = new TempWorkspace();

		workspace.WriteIntent(MalformedIntent);

		var result = await RunVerify(workspace);

		result.ExitCode.Should().Be(0);
	}

	private static string IntentFor(string className, string behavior)
	{
		return $$"""
			{
			  "why": "End-to-end fixture for the verify command.",
			  "class": "{{className}}",
			  "responsibility": "Exist so verify has a production class to check.",
			  "behaviors": [ { "behavior": "{{behavior}}" } ]
			}
			""";
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
