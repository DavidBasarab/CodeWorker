using FatCat.CodeWorker.Commands.Run;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class RunCommandTests
{
	private readonly ILogger logger;
	private readonly RunCommand command;

	public RunCommandTests()
	{
		logger = A.Fake<ILogger>();

		command = new RunCommand(logger);
	}

	[Fact]
	public async Task LogThatTheRunCommandIsStarting()
	{
		await command.Execute(Array.Empty<string>());

		A.CallTo(() => logger.Information("Starting task runner")).MustHaveHappenedOnceExactly();
	}
}
