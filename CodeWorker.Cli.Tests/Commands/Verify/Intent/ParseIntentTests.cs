using FatCat.CodeWorker.Cli.Commands.Verify;
using FatCat.CodeWorker.Cli.Commands.Verify.Intent;
using FatCat.Toolkit;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public class ParseIntentTests
{
	private const string SampleJson = """
		{
		  "why": "Tasks that exceed the token limit must be re-queued as pending, not failed.",
		  "class": "TokenLimitHeuristic",
		  "responsibility": "Classify a ProcessResult as TokenLimited when the transcript shows the token-limit signature.",
		  "behaviors": [
		    { "behavior": "Returns TokenLimited when the transcript contains the limit marker", "test": "ReturnTokenLimitedWhenMarkerPresent" },
		    { "behavior": "Returns null when no marker is present" }
		  ]
		}
		""";

	private const string MixedCaseJson = """
		{
		  "Why": "Tasks that exceed the token limit must be re-queued as pending, not failed.",
		  "CLASS": "TokenLimitHeuristic",
		  "RESPONSIBILITY": "Classify a ProcessResult as TokenLimited when the transcript shows the token-limit signature.",
		  "Behaviors": [
		    { "BEHAVIOR": "Returns null when no marker is present" }
		  ]
		}
		""";

	private readonly VerifyArgumentsResult arguments;
	private readonly IFileSystemTools fileSystemTools;
	private readonly ParseIntent parseIntent;
	private readonly IntentResult validatedResult;
	private readonly IValidateIntent validateIntent;
	private IntentContract capturedIntent;
	private string currentJson = SampleJson;

	public ParseIntentTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		validateIntent = A.Fake<IValidateIntent>();
		validatedResult = new IntentResult { IsValid = true, Error = IntentValidationError.None };

		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).ReturnsLazily(() => currentJson);

		A.CallTo(() => validateIntent.Validate(A<IntentContract>._, A<string>._))
			.ReturnsLazily(
				(IntentContract intent, string _) =>
				{
					capturedIntent = intent;

					return validatedResult;
				}
			);

		arguments = new VerifyArgumentsResult
		{
			IsValid = true,
			IntentPath = Faker.Create<string>(),
			ProductionPath = Faker.Create<string>(),
			TestsPath = Faker.Create<string>(),
		};

		parseIntent = new ParseIntent(fileSystemTools, validateIntent);
	}

	[Fact]
	public async Task ReadTheIntentFileFromTheIntentPath()
	{
		await parseIntent.Parse(arguments);

		A.CallTo(() => fileSystemTools.ReadAllText(arguments.IntentPath)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeserializeTheWhy()
	{
		await parseIntent.Parse(arguments);

		capturedIntent.Why.Should().Be("Tasks that exceed the token limit must be re-queued as pending, not failed.");
	}

	[Fact]
	public async Task DeserializeTheClass()
	{
		await parseIntent.Parse(arguments);

		capturedIntent.Class.Should().Be("TokenLimitHeuristic");
	}

	[Fact]
	public async Task DeserializeTheResponsibility()
	{
		await parseIntent.Parse(arguments);

		capturedIntent
			.Responsibility.Should()
			.Be("Classify a ProcessResult as TokenLimited when the transcript shows the token-limit signature.");
	}

	[Fact]
	public async Task DeserializeEveryBehavior()
	{
		await parseIntent.Parse(arguments);

		capturedIntent.Behaviors.Should().HaveCount(2);
	}

	[Fact]
	public async Task DeserializeTheBehaviorText()
	{
		await parseIntent.Parse(arguments);

		capturedIntent.Behaviors[0].Behavior.Should().Be("Returns TokenLimited when the transcript contains the limit marker");
	}

	[Fact]
	public async Task DeserializeTheTestLink()
	{
		await parseIntent.Parse(arguments);

		capturedIntent.Behaviors[0].Test.Should().Be("ReturnTokenLimitedWhenMarkerPresent");
	}

	[Fact]
	public async Task LeaveTheTestNullWhenOmitted()
	{
		await parseIntent.Parse(arguments);

		capturedIntent.Behaviors[1].Test.Should().BeNull();
	}

	[Fact]
	public async Task MatchPropertyNamesCaseInsensitively()
	{
		currentJson = MixedCaseJson;

		await parseIntent.Parse(arguments);

		capturedIntent.Class.Should().Be("TokenLimitHeuristic");
	}

	[Fact]
	public async Task ValidateAgainstTheProductionPath()
	{
		await parseIntent.Parse(arguments);

		A.CallTo(() => validateIntent.Validate(A<IntentContract>._, arguments.ProductionPath)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnTheValidationResult()
	{
		var result = await parseIntent.Parse(arguments);

		result.Should().BeSameAs(validatedResult);
	}

	[Fact]
	public async Task ReturnIntentFileUnreadableWhenReadThrowsIOException()
	{
		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).Throws(new IOException("locked"));

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.IntentFileUnreadable);
	}

	[Fact]
	public async Task ReturnIntentFileUnreadableWhenAccessIsDenied()
	{
		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).Throws(new UnauthorizedAccessException("denied"));

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.IntentFileUnreadable);
	}

	[Fact]
	public async Task ReturnIntentFileUnreadableWhenTheFileIsEmptyOrMissing()
	{
		currentJson = string.Empty;

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.IntentFileUnreadable);
	}

	[Fact]
	public async Task NameTheIntentPathWhenUnreadable()
	{
		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).Throws(new IOException("locked"));

		var result = await parseIntent.Parse(arguments);

		result.Message.Should().Contain(arguments.IntentPath);
	}

	[Fact]
	public async Task ReturnMalformedIntentJsonForInvalidSyntax()
	{
		currentJson = "{ not json";

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.MalformedIntentJson);
	}

	[Fact]
	public async Task ReturnIntentFileUnreadableForWhitespaceContent()
	{
		currentJson = "   \n";

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.IntentFileUnreadable);
	}

	[Fact]
	public async Task NotValidateWhenTheFileIsEmptyOrMissing()
	{
		currentJson = string.Empty;

		await parseIntent.Parse(arguments);

		A.CallTo(() => validateIntent.Validate(A<IntentContract>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotCheckFileExistence()
	{
		await parseIntent.Parse(arguments);

		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnMalformedIntentJsonForANullPayload()
	{
		currentJson = "null";

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.MalformedIntentJson);
	}

	[Fact]
	public async Task ReturnMalformedIntentJsonForAnArrayPayload()
	{
		currentJson = "[]";

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.MalformedIntentJson);
	}

	[Fact]
	public async Task ReturnMalformedIntentJsonForAnUnknownProperty()
	{
		currentJson = SampleJson.Replace("\"test\":", "\"tests\":");

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.MalformedIntentJson);
	}

	[Fact]
	public async Task NotValidateWhenTheJsonIsMalformed()
	{
		currentJson = "{ not json";

		await parseIntent.Parse(arguments);

		A.CallTo(() => validateIntent.Validate(A<IntentContract>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotValidateWhenTheFileIsUnreadable()
	{
		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).Throws(new IOException("locked"));

		await parseIntent.Parse(arguments);

		A.CallTo(() => validateIntent.Validate(A<IntentContract>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnInvalidWhenTheJsonIsMalformed()
	{
		currentJson = "{ not json";

		var result = await parseIntent.Parse(arguments);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task ReturnInvalidWhenTheFileIsUnreadable()
	{
		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).Throws(new IOException("locked"));

		var result = await parseIntent.Parse(arguments);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task NameTheIntentPathWhenTheFileIsEmptyOrMissing()
	{
		currentJson = string.Empty;

		var result = await parseIntent.Parse(arguments);

		result.Message.Should().Contain(arguments.IntentPath);
	}

	[Fact]
	public async Task NameTheIntentPathWhenTheJsonIsMalformed()
	{
		currentJson = "{ not json";

		var result = await parseIntent.Parse(arguments);

		result.Message.Should().Contain(arguments.IntentPath);
	}

	[Fact]
	public async Task LeaveTheIntentNullWhenTheJsonIsMalformed()
	{
		currentJson = "{ not json";

		var result = await parseIntent.Parse(arguments);

		result.Intent.Should().BeNull();
	}

	[Fact]
	public async Task LeaveTheIntentNullWhenTheFileIsUnreadable()
	{
		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).Throws(new IOException("locked"));

		var result = await parseIntent.Parse(arguments);

		result.Intent.Should().BeNull();
	}

	[Fact]
	public async Task ReturnMalformedIntentJsonForAStringPayload()
	{
		currentJson = "\"text\"";

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.MalformedIntentJson);
	}

	[Fact]
	public async Task ReturnMalformedIntentJsonForANumberPayload()
	{
		currentJson = "42";

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.MalformedIntentJson);
	}

	[Fact]
	public async Task ReturnMalformedIntentJsonForAWronglyTypedProperty()
	{
		currentJson = """{ "why": 42 }""";

		var result = await parseIntent.Parse(arguments);

		result.Error.Should().Be(IntentValidationError.MalformedIntentJson);
	}

	[Fact]
	public async Task LetUnexpectedExceptionsPropagate()
	{
		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).Throws(new InvalidOperationException("unexpected"));

		await FluentActions.Awaiting(() => parseIntent.Parse(arguments)).Should().ThrowAsync<InvalidOperationException>();
	}
}
