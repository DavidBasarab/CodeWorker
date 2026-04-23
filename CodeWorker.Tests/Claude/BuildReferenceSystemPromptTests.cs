using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;

namespace Testing.FatCat.CodeWorker.Claude;

public class BuildReferenceSystemPromptTests
{
	private readonly BuildReferenceSystemPrompt buildReferenceSystemPrompt;

	public BuildReferenceSystemPromptTests()
	{
		buildReferenceSystemPrompt = new BuildReferenceSystemPrompt();
	}

	[Fact]
	public void IncludeReferenceFileNameAsHeader()
	{
		var referenceFiles = new List<ReferenceFile>
		{
			new() { Name = "context.md", Content = "some content" },
		};

		var result = buildReferenceSystemPrompt.Build(referenceFiles);

		result.Should().Contain("## Reference: context.md");
	}

	[Fact]
	public void IncludeReferenceFileContent()
	{
		var referenceFiles = new List<ReferenceFile>
		{
			new() { Name = "context.md", Content = "important context here" },
		};

		var result = buildReferenceSystemPrompt.Build(referenceFiles);

		result.Should().Contain("important context here");
	}

	[Fact]
	public void IncludeMultipleReferenceFiles()
	{
		var referenceFiles = new List<ReferenceFile>
		{
			new() { Name = "first.md", Content = "first content" },
			new() { Name = "second.md", Content = "second content" },
		};

		var result = buildReferenceSystemPrompt.Build(referenceFiles);

		result.Should().Contain("## Reference: first.md");
		result.Should().Contain("first content");
		result.Should().Contain("## Reference: second.md");
		result.Should().Contain("second content");
	}

	[Fact]
	public void EscapeDoubleQuotesInContent()
	{
		var referenceFiles = new List<ReferenceFile>
		{
			new() { Name = "test.md", Content = "value is \"quoted\"" },
		};

		var result = buildReferenceSystemPrompt.Build(referenceFiles);

		result.Should().Contain("value is \\\"quoted\\\"");
		result.Should().NotContain("value is \"quoted\"");
	}

	[Fact]
	public void TrimTrailingWhitespace()
	{
		var referenceFiles = new List<ReferenceFile>
		{
			new() { Name = "test.md", Content = "content" },
		};

		var result = buildReferenceSystemPrompt.Build(referenceFiles);

		result.Should().Be(result.TrimEnd());
	}

	[Fact]
	public void ReturnEmptyStringForEmptyList()
	{
		var referenceFiles = new List<ReferenceFile>();

		var result = buildReferenceSystemPrompt.Build(referenceFiles);

		result.Should().BeEmpty();
	}
}
