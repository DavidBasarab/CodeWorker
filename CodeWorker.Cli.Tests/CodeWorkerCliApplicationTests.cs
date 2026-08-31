using FatCat.CodeWorker.Cli.Commands;
using Serilog;

namespace Testing.FatCat.CodeWorker.Cli;

public class CodeWorkerCliApplicationTests
{
	private readonly IProcessArguments processArguments;
	private readonly ILogger logger;
	private readonly CodeWorkerCliApplication application;

	public CodeWorkerCliApplicationTests()
	{
		processArguments = A.Fake<IProcessArguments>();
		logger = A.Fake<ILogger>();

		application = new CodeWorkerCliApplication(processArguments, logger);
	}

	[Fact]
	public async Task ProcessTheArguments()
	{
		var args = new[] { "greet", "world" };

		await application.Run(args);

		A.CallTo(() => processArguments.Process(args)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWelcomeMessage()
	{
		await application.Run(new[] { "greet" });

		A.CallTo(() => logger.Information("Welcome to Code Worker CLI")).MustHaveHappenedOnceExactly();
	}
}
