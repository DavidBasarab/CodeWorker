using FatCat.CodeWorker.Cli.Commands.Verify;
using Serilog;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify;

public class VerifyCommandTests
{
	private readonly ILogger logger;
	private readonly VerifyCommand verifyCommand;

	public VerifyCommandTests()
	{
		logger = A.Fake<ILogger>();

		verifyCommand = new VerifyCommand(logger);
	}

	[Fact]
	public async Task CompleteWithoutError()
	{
		var args = Faker.Create<string[]>();

		await FluentActions.Awaiting(() => verifyCommand.Execute(args)).Should().NotThrowAsync();
	}
}
