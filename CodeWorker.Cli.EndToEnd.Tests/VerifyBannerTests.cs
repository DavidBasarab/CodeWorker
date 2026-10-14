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
