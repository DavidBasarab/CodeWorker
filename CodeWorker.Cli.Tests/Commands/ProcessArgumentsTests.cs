using FatCat.CodeWorker.Cli.Commands;
using Serilog;

namespace Testing.FatCat.CodeWorker.Cli.Commands;

public class ProcessArgumentsTests
{
	private readonly ILogger logger;
	private readonly ProcessArguments processArguments;

	public ProcessArgumentsTests()
	{
		logger = A.Fake<ILogger>();

		processArguments = new ProcessArguments(logger);
	}

	[Fact]
	public async Task LogWhenNoArgumentsProvided()
	{
		await processArguments.Process(Array.Empty<string>());

		A.CallTo(() => logger.Information("No arguments provided")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogArgumentCountWhenArgumentsProvided()
	{
		var args = new[] { "greet", "world" };

		await processArguments.Process(args);

		A.CallTo(() => logger.Information(A<string>._, 2, A<string>._)).MustHaveHappenedOnceExactly();
	}
}
