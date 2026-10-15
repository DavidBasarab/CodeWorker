using FatCat.CodeWorker.Cli.Commands.Verify.Intent;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public class ValidateIntentTests
{
	private const string ProductionPath = @"C:\Repo\Src\TokenLimitHeuristic.cs";

	private readonly ValidateIntent validateIntent;

	public ValidateIntentTests()
	{
		validateIntent = new ValidateIntent();
	}

	private static IntentContract ValidIntent()
	{
		return new IntentContract
		{
			Why = Faker.Create<string>(),
			Class = "TokenLimitHeuristic",
			Responsibility = Faker.Create<string>(),
			Behaviors =
			[
				new IntentBehavior
				{
					Behavior = "Returns TokenLimited when the marker is present",
					Test = "ReturnTokenLimitedWhenMarkerPresent",
				},
			],
		};
	}

	private static IntentContract IntentWithBehavior(string behavior)
	{
		var intent = ValidIntent();

		intent.Behaviors[0].Behavior = behavior;

		return intent;
	}

	private static IntentContract IntentWithTest(string test)
	{
		var intent = ValidIntent();

		intent.Behaviors[0].Test = test;

		return intent;
	}

	[Fact]
	public void ReturnValidForACompleteIntent()
	{
		var result = validateIntent.Validate(ValidIntent(), ProductionPath);

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void ReturnNoneErrorForACompleteIntent()
	{
		var result = validateIntent.Validate(ValidIntent(), ProductionPath);

		result.Error.Should().Be(IntentValidationError.None);
	}

	[Fact]
	public void ReturnNoMessageForACompleteIntent()
	{
		var result = validateIntent.Validate(ValidIntent(), ProductionPath);

		result.Message.Should().BeNull();
	}

	[Fact]
	public void ExplainAMissingWhy()
	{
		var intent = ValidIntent();

		intent.Why = null;

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Message.Should().Be("Intent is missing \"why\" — the reason this code needs to exist.");
	}

	[Fact]
	public void ExplainAMissingClass()
	{
		var intent = ValidIntent();

		intent.Class = null;

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Message.Should().Be("Intent is missing \"class\" — the production class this intent describes.");
	}

	[Fact]
	public void ExplainAMissingResponsibility()
	{
		var intent = ValidIntent();

		intent.Responsibility = null;

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Message.Should().Be("Intent is missing \"responsibility\" — what this class is responsible for.");
	}

	[Fact]
	public void ExplainMissingBehaviors()
	{
		var intent = ValidIntent();

		intent.Behaviors = [];

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Message.Should().Be("Intent declares no behaviors — at least one observable behavior is required.");
	}

	[Fact]
	public void NameTheBehaviorNumberWhenEmpty()
	{
		var intent = ValidIntent();

		intent.Behaviors.Add(new IntentBehavior { Behavior = " " });

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Message.Should().Be("Intent behavior 2 is empty.");
	}

	[Fact]
	public void NameTheTestWhenTestNameIsInvalid()
	{
		var result = validateIntent.Validate(IntentWithTest("1ReturnX"), ProductionPath);

		result.Message.Should().Be("Intent behavior 1 names test \"1ReturnX\", which is not a valid test method name.");
	}

	[Fact]
	public void ReturnTheIntentWhenValid()
	{
		var intent = ValidIntent();

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Intent.Should().BeSameAs(intent);
	}

	[Fact]
	public void ReturnMissingWhyWhenWhyIsBlank()
	{
		var intent = ValidIntent();

		intent.Why = "  ";

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.MissingWhy);
	}

	[Fact]
	public void ReturnMissingClassWhenClassIsNull()
	{
		var intent = ValidIntent();

		intent.Class = null;

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.MissingClass);
	}

	[Fact]
	public void ReturnClassDoesNotMatchProductionFileWhenNamesDiffer()
	{
		var intent = ValidIntent();

		intent.Class = "Other";

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.ClassDoesNotMatchProductionFile);
	}

	[Fact]
	public void ReturnClassDoesNotMatchProductionFileWhenCaseDiffers()
	{
		var intent = ValidIntent();

		intent.Class = "tokenLimitHeuristic";

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.ClassDoesNotMatchProductionFile);
	}

	[Fact]
	public void AcceptAClassWithSurroundingWhitespace()
	{
		var intent = ValidIntent();

		intent.Class = " TokenLimitHeuristic ";

		var result = validateIntent.Validate(intent, ProductionPath);

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void NameTheExpectedClassWhenClassDoesNotMatch()
	{
		var intent = ValidIntent();

		intent.Class = "Other";

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Message.Should().Contain("expected \"TokenLimitHeuristic\"");
	}

	[Fact]
	public void ReturnMissingResponsibilityWhenResponsibilityIsEmpty()
	{
		var intent = ValidIntent();

		intent.Responsibility = "";

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.MissingResponsibility);
	}

	[Fact]
	public void ReturnNoBehaviorsWhenBehaviorsIsEmpty()
	{
		var intent = ValidIntent();

		intent.Behaviors = [];

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.NoBehaviors);
	}

	[Fact]
	public void ReturnNoBehaviorsWhenBehaviorsIsNull()
	{
		var intent = ValidIntent();

		intent.Behaviors = null;

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.NoBehaviors);
	}

	[Fact]
	public void ReturnEmptyBehaviorWhenABehaviorIsBlank()
	{
		var result = validateIntent.Validate(IntentWithBehavior("   "), ProductionPath);

		result.Error.Should().Be(IntentValidationError.EmptyBehavior);
	}

	[Fact]
	public void ReturnEmptyBehaviorWhenABehaviorEntryIsNull()
	{
		var intent = ValidIntent();

		intent.Behaviors = [null];

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.EmptyBehavior);
	}

	[Fact]
	public void ReturnNonObservableBehaviorForAnImplementationDetail()
	{
		var result = validateIntent.Validate(IntentWithBehavior("Uses a switch on the outcome"), ProductionPath);

		result.Error.Should().Be(IntentValidationError.NonObservableBehavior);
	}

	[Fact]
	public void NameTheBehaviorNumberWhenNotObservable()
	{
		var intent = ValidIntent();

		intent.Behaviors.Add(new IntentBehavior { Behavior = "Uses a switch on the outcome" });

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Message.Should().Contain("behavior 2");
	}

	[Fact]
	public void AcceptAnObservableVerbCaseInsensitively()
	{
		var result = validateIntent.Validate(IntentWithBehavior("returns X when Y"), ProductionPath);

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void AcceptANegatedObservableBehavior()
	{
		var result = validateIntent.Validate(IntentWithBehavior("Does not call the logger when Y"), ProductionPath);

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void AcceptANeverPrefixedObservableBehavior()
	{
		var result = validateIntent.Validate(IntentWithBehavior("Never writes the file when Y"), ProductionPath);

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void ReturnNonObservableBehaviorForABaseFormVerbWithoutANegation()
	{
		var result = validateIntent.Validate(IntentWithBehavior("Return X when Y"), ProductionPath);

		result.Error.Should().Be(IntentValidationError.NonObservableBehavior);
	}

	[Fact]
	public void ReturnNonObservableBehaviorForANegatedImplementationDetail()
	{
		var result = validateIntent.Validate(IntentWithBehavior("Does not use a switch"), ProductionPath);

		result.Error.Should().Be(IntentValidationError.NonObservableBehavior);
	}

	[Fact]
	public void ReturnNonObservableBehaviorWhenOnlyANegationPrefix()
	{
		var result = validateIntent.Validate(IntentWithBehavior("Does not"), ProductionPath);

		result.Error.Should().Be(IntentValidationError.NonObservableBehavior);
	}

	[Fact]
	public void AcceptABehaviorWithoutATest()
	{
		var result = validateIntent.Validate(IntentWithTest(null), ProductionPath);

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void ReturnInvalidTestNameWhenTestHasSpaces()
	{
		var result = validateIntent.Validate(IntentWithTest("returns x when y"), ProductionPath);

		result.Error.Should().Be(IntentValidationError.InvalidTestName);
	}

	[Fact]
	public void ReturnInvalidTestNameWhenTestIsEmpty()
	{
		var result = validateIntent.Validate(IntentWithTest(""), ProductionPath);

		result.Error.Should().Be(IntentValidationError.InvalidTestName);
	}

	[Fact]
	public void ReturnInvalidTestNameWhenTestStartsWithADigit()
	{
		var result = validateIntent.Validate(IntentWithTest("1ReturnX"), ProductionPath);

		result.Error.Should().Be(IntentValidationError.InvalidTestName);
	}

	[Fact]
	public void ReturnInvalidTestNameWhenTestEndsWithANewline()
	{
		var result = validateIntent.Validate(IntentWithTest("ReturnX\n"), ProductionPath);

		result.Error.Should().Be(IntentValidationError.InvalidTestName);
	}

	[Fact]
	public void CheckWhyBeforeClass()
	{
		var intent = ValidIntent();

		intent.Why = " ";
		intent.Class = null;

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.MissingWhy);
	}

	[Fact]
	public void CheckBehaviorTextBeforeTestName()
	{
		var intent = IntentWithBehavior("Uses a switch on the outcome");

		intent.Behaviors[0].Test = "not a test name";

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Error.Should().Be(IntentValidationError.NonObservableBehavior);
	}

	[Fact]
	public void ReturnInvalidForAnyFailure()
	{
		var intent = ValidIntent();

		intent.Why = null;

		var result = validateIntent.Validate(intent, ProductionPath);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void KeepTheIntentOnFailure()
	{
		var intent = ValidIntent();

		intent.Why = null;

		var result = validateIntent.Validate(intent, ProductionPath);

		result.Intent.Should().BeSameAs(intent);
	}
}
