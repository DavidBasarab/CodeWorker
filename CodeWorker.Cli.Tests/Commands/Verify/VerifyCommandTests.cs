using FatCat.CodeWorker.Cli.Commands.Verify;
using FatCat.CodeWorker.Cli.Commands.Verify.Gates;
using FatCat.CodeWorker.Cli.Commands.Verify.Intent;
using Serilog;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify;

public class VerifyCommandTests
{
	private const string IntentSummaryTemplate = "verify: intent for {Class} declares {BehaviorCount} behaviors";
	private const string PipelineSummaryTemplate =
		"verify: gate pipeline finished with {GateCount} gates, blocked: {IsBlocked}";

	private readonly IParseVerifyArguments parseVerifyArguments;
	private readonly IParseIntent parseIntent;
	private readonly IRunGatePipeline runGatePipeline;
	private readonly ILogger logger;
	private readonly VerifyCommand verifyCommand;

	private VerifyArgumentsResult currentResult;
	private IntentResult currentIntentResult;
	private GatePipelineResult currentPipelineResult;

	public VerifyCommandTests()
	{
		parseVerifyArguments = A.Fake<IParseVerifyArguments>();
		parseIntent = A.Fake<IParseIntent>();
		runGatePipeline = A.Fake<IRunGatePipeline>();
		logger = A.Fake<ILogger>();

		currentResult = ValidResult();
		currentIntentResult = ValidIntentResult();
		currentPipelineResult = new GatePipelineResult { IsBlocked = false, GateResults = [] };

		A.CallTo(() => parseVerifyArguments.Parse(A<string[]>._)).ReturnsLazily(() => currentResult);
		A.CallTo(() => parseIntent.Parse(A<VerifyArgumentsResult>._)).ReturnsLazily(() => currentIntentResult);
		A.CallTo(() => runGatePipeline.Run(A<VerificationContext>._)).ReturnsLazily(() => currentPipelineResult);

		verifyCommand = new VerifyCommand(parseVerifyArguments, parseIntent, runGatePipeline, logger);
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
	public void CompleteWhenValid()
	{
		currentResult = ValidResult();

		Func<Task> act = () => verifyCommand.Execute(Faker.Create<string[]>());

		act.Should().NotThrowAsync();
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

	[Fact]
	public async Task RunTheGatePipelineWhenTheIntentIsValid()
	{
		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => runGatePipeline.Run(A<VerificationContext>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotRunTheGatePipelineWhenArgumentsAreInvalid()
	{
		currentResult = InvalidResult();

		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => runGatePipeline.Run(A<VerificationContext>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotRunTheGatePipelineWhenTheIntentIsInvalid()
	{
		currentIntentResult = InvalidIntentResult();

		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => runGatePipeline.Run(A<VerificationContext>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task PassTheParsedPathsAndIntentInTheContext()
	{
		VerificationContext capturedContext = null;

		A.CallTo(() => runGatePipeline.Run(A<VerificationContext>._))
			.Invokes((VerificationContext context) => capturedContext = context)
			.ReturnsLazily(() => currentPipelineResult);

		await verifyCommand.Execute(Faker.Create<string[]>());

		capturedContext
			.Should()
			.BeEquivalentTo(
				new VerificationContext
				{
					IntentPath = currentResult.IntentPath,
					ProductionPath = currentResult.ProductionPath,
					TestsPath = currentResult.TestsPath,
					Intent = currentIntentResult.Intent,
				}
			);
	}

	[Fact]
	public async Task LogThePipelineSummary()
	{
		currentPipelineResult = new GatePipelineResult
		{
			IsBlocked = true,
			GateResults = [Faker.Create<GateResult>(), Faker.Create<GateResult>()],
		};

		await verifyCommand.Execute(Faker.Create<string[]>());

		A.CallTo(() => logger.Information(PipelineSummaryTemplate, 2, true)).MustHaveHappenedOnceExactly();
	}
}
