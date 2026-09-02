using FatCat.CodeWorker.Cli.Commands.Verify;
using FatCat.Toolkit;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify;

public class ParseVerifyArgumentsTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ParseVerifyArguments parseVerifyArguments;

	public ParseVerifyArgumentsTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();

		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).Returns(true);

		parseVerifyArguments = new ParseVerifyArguments(fileSystemTools);
	}

	private static string[] WellFormedArgs()
	{
		return ["verify", "--intent", "intent.json", "--production", "Foo.cs", "--tests", "FooTests.cs"];
	}

	[Fact]
	public void ReturnValidWhenAllFlagsPresentAndFilesExist()
	{
		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void ReturnTheIntentPathWhenValid()
	{
		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.IntentPath.Should().Be("intent.json");
	}

	[Fact]
	public void ReturnTheProductionPathWhenValid()
	{
		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.ProductionPath.Should().Be("Foo.cs");
	}

	[Fact]
	public void ReturnTheTestsPathWhenValid()
	{
		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.TestsPath.Should().Be("FooTests.cs");
	}

	[Fact]
	public void ReturnNoneErrorWhenValid()
	{
		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.Error.Should().Be(VerifyUsageError.None);
	}

	[Fact]
	public void ReturnNoArgumentsWhenNoFlagsPresent()
	{
		var result = parseVerifyArguments.Parse(["verify"]);

		result.Error.Should().Be(VerifyUsageError.NoArguments);
	}

	[Fact]
	public void ReturnNoArgumentsWhenArgsEmpty()
	{
		var result = parseVerifyArguments.Parse([]);

		result.Error.Should().Be(VerifyUsageError.NoArguments);
	}

	[Fact]
	public void ReturnMissingIntentFlagWhenIntentAbsent()
	{
		var result = parseVerifyArguments.Parse(["verify", "--production", "Foo.cs", "--tests", "FooTests.cs"]);

		result.Error.Should().Be(VerifyUsageError.MissingIntentFlag);
	}

	[Fact]
	public void ReturnMissingIntentFlagWhenIntentHasNoValue()
	{
		var result = parseVerifyArguments.Parse(["verify", "--intent", "--production", "Foo.cs", "--tests", "FooTests.cs"]);

		result.Error.Should().Be(VerifyUsageError.MissingIntentFlag);
	}

	[Fact]
	public void ResolveTheLaterValueWhenFlagRepeatedWithoutValueFirst()
	{
		var result = parseVerifyArguments.Parse([
			"verify",
			"--intent",
			"--intent",
			"intent.json",
			"--production",
			"Foo.cs",
			"--tests",
			"FooTests.cs",
		]);

		result.IntentPath.Should().Be("intent.json");
	}

	[Fact]
	public void ReturnMissingIntentFlagWhenAllFlagsPresentWithoutValues()
	{
		var result = parseVerifyArguments.Parse(["verify", "--intent", "--production", "--tests"]);

		result.Error.Should().Be(VerifyUsageError.MissingIntentFlag);
	}

	[Fact]
	public void ReturnMissingProductionFlagWhenProductionAbsent()
	{
		var result = parseVerifyArguments.Parse(["verify", "--intent", "intent.json", "--tests", "FooTests.cs"]);

		result.Error.Should().Be(VerifyUsageError.MissingProductionFlag);
	}

	[Fact]
	public void ReturnMissingTestsFlagWhenTestsAbsent()
	{
		var result = parseVerifyArguments.Parse(["verify", "--intent", "intent.json", "--production", "Foo.cs"]);

		result.Error.Should().Be(VerifyUsageError.MissingTestsFlag);
	}

	[Fact]
	public void ReturnIntentFileNotFoundWhenIntentMissing()
	{
		A.CallTo(() => fileSystemTools.FileExists("intent.json")).Returns(false);

		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.Error.Should().Be(VerifyUsageError.IntentFileNotFound);
	}

	[Fact]
	public void ReturnProductionFileNotFoundWhenProductionMissing()
	{
		A.CallTo(() => fileSystemTools.FileExists("Foo.cs")).Returns(false);

		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.Error.Should().Be(VerifyUsageError.ProductionFileNotFound);
	}

	[Fact]
	public void ReturnTestsFileNotFoundWhenTestsMissing()
	{
		A.CallTo(() => fileSystemTools.FileExists("FooTests.cs")).Returns(false);

		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.Error.Should().Be(VerifyUsageError.TestsFileNotFound);
	}

	[Fact]
	public void ReturnInvalidWhenIntentFileMissing()
	{
		A.CallTo(() => fileSystemTools.FileExists("intent.json")).Returns(false);

		var result = parseVerifyArguments.Parse(WellFormedArgs());

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void PopulateAMessageWhenInvalid()
	{
		var result = parseVerifyArguments.Parse(["verify", "--production", "Foo.cs", "--tests", "FooTests.cs"]);

		result.Message.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void CheckIntentFlagPrecedesFileChecks()
	{
		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).Returns(false);

		var result = parseVerifyArguments.Parse(["verify", "--production", "Foo.cs", "--tests", "FooTests.cs"]);

		result.Error.Should().Be(VerifyUsageError.MissingIntentFlag);
	}
}
