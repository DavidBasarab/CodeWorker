using FatCat.CodeWorker.Cli.Commands.Verify;
using FatCat.CodeWorker.Cli.Commands.Verify.Intent;
using Serilog;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify;

public class VerifyCommandTests
{
	private const string IntentSummaryTemplate = "verify: intent for {Class} declares {BehaviorCount} behaviors";

	private readonly IParseVerifyArguments parseVerifyArguments;
	private readonly IParseIntent parseIntent;
	private readonly ILogger logger;
	private readonly VerifyCommand verifyCommand;

	private VerifyArgumentsResult currentResult;
	private IntentResult currentIntentResult;

	public VerifyCommandTests()
	{
		parseVerifyArguments = A.Fake<IParseVerifyArguments>();
		parseIntent = A.Fake<IParseIntent>();
		logger = A.Fake<ILogger>();

		currentResult = ValidResult();
		currentIntentResult = ValidIntentResult();

		A.CallTo(() => parseVerifyArguments.Parse(A<string[]>._)).ReturnsLazily(() => currentResult);
		A.CallTo(() => parseIntent.Parse(A<VerifyArgumentsResult>._)).ReturnsLazily(() => currentIntentResult);

		verifyCommand = new VerifyCommand(parseVerifyArguments, parseIntent, logger);
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

	private static IntentResult ValidIntentResult()
	{
		return new IntentResult
		{
			IsValid = true,
			Error = IntentValidationError.None,
			Intent = new IntentContract
			{
				Why = Faker.Create<string>(),
				Class = Faker.Create<string>(),
				Responsibility = Faker.Create<string>(),
				Behaviors = [Faker.Create<IntentBehavior>(), Faker.Create<IntentBehavior>()],
			},
		};
	}

	private static IntentResult InvalidIntentResult()
	{
		return new IntentResult
		{
			IsValid = false,
			Error = IntentValidationError.MalformedIntentJson,
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

	[Fact]
	public async Task ParseTheIntentWithTheParsedArguments()
	{
		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => parseIntent.Parse(currentResult)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotParseTheIntentWhenArgumentsAreInvalid()
	{
		currentResult = InvalidResult();

		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => parseIntent.Parse(A<VerifyArgumentsResult>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task LogTheIntentErrorWhenIntentIsInvalid()
	{
		currentIntentResult = InvalidIntentResult();

		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => logger.Error(A<string>._, currentIntentResult.Message)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogTheIntentClassAndBehaviorCountWhenValid()
	{
		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => logger.Information(IntentSummaryTemplate, currentIntentResult.Intent.Class, 2))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotLogTheIntentSummaryWhenIntentIsInvalid()
	{
		currentIntentResult = InvalidIntentResult();

		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => logger.Information(IntentSummaryTemplate, A<string>._, A<int>._)).MustNotHaveHappened();
	}
}
