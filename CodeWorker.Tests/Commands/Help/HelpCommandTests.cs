using FatCat.CodeWorker.Commands.Help;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Help;

public class HelpCommandTests
{
	private readonly ILogger logger;
	private readonly HelpCommand command;

	public HelpCommandTests()
	{
		logger = A.Fake<ILogger>();

		command = new HelpCommand(logger);
	}

	[Fact]
	public async Task LogUsageHeader()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("FatCatCodeWorker"))).MustHaveHappened();
	}

	[Fact]
	public async Task LogSetupCommand()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("setup"))).MustHaveHappened();
	}

	[Fact]
	public async Task LogTrackCommand()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("track"))).MustHaveHappened();
	}

	[Fact]
	public async Task LogUntrackCommand()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("untrack"))).MustHaveHappened();
	}

	[Fact]
	public async Task LogListCommand()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("list"))).MustHaveHappened();
	}

	[Fact]
	public async Task LogInfoCommand()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("info"))).MustHaveHappened();
	}

	[Fact]
	public async Task LogRunTaskCommand()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("run-task"))).MustHaveHappened();
	}

	[Fact]
	public async Task LogHelpCommand()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("help"))).MustHaveHappened();
	}

	[Fact]
	public async Task LogNoArgumentsBehavior()
	{
		await command.Execute(new[] { "help" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("no arguments"))).MustHaveHappened();
	}
}
