using FatCat.CodeWorker.Cli.Commands.Verify;
using Serilog;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify;

public class VerifyCommandTests
{
	private readonly IParseVerifyArguments parseVerifyArguments;
	private readonly ILogger logger;
	private readonly VerifyCommand verifyCommand;

	private VerifyArgumentsResult currentResult;

	public VerifyCommandTests()
	{
		parseVerifyArguments = A.Fake<IParseVerifyArguments>();
		logger = A.Fake<ILogger>();

		currentResult = ValidResult();

		A.CallTo(() => parseVerifyArguments.Parse(A<string[]>._)).ReturnsLazily(() => currentResult);

		verifyCommand = new VerifyCommand(parseVerifyArguments, logger);
	}

	private static VerifyArgumentsResult ValidResult()
	{
		return new VerifyArgumentsResult
		{
			IsValid = true,
			Error = VerifyUsageError.None,
			IntentPath = Faker.Create<string>(),
			ProductionPath = Faker.Create<string>(),
			TestsPath = Faker.Create<string>(),
		};
	}

	private static VerifyArgumentsResult InvalidResult()
	{
		return new VerifyArgumentsResult
		{
			IsValid = false,
			Error = VerifyUsageError.MissingIntentFlag,
			Message = Faker.Create<string>(),
		};
	}

	[Fact]
	public async Task ParseTheArguments()
	{
		var args = Faker.Create<string[]>();

		await verifyCommand.Execute(args);

		A.CallTo(() => parseVerifyArguments.Parse(args)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogTheUsageErrorWhenInvalid()
	{
		currentResult = InvalidResult();

		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => logger.Error(A<string>._, currentResult.Message)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotLogAnErrorWhenValid()
	{
		currentResult = ValidResult();

		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => logger.Error(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task CompleteWhenValid()
	{
		currentResult = ValidResult();

		await FluentActions.Awaiting(() => verifyCommand.Execute(Faker.Create<string[]>())).Should().NotThrowAsync();
	}
}
