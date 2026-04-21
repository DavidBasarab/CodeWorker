using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Process;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class GenerateBlockedExplanationTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly GenerateBlockedExplanation generateBlockedExplanation;
	private ProcessResult processResult;
	private string writtenContent;

	public GenerateBlockedExplanationTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		processResult = new ProcessResult
		{
			ExitCode = 1,
			OutputLines = new List<string> { "Some output from Claude" },
			ErrorLines = new List<string> { "Some error occurred" },
		};

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>.Ignored, A<string>.Ignored))
			.Invokes((string path, string content) => writtenContent = content)
			.Returns(Task.CompletedTask);

		generateBlockedExplanation = new GenerateBlockedExplanation(fileSystemTools, logger);
	}

	[Fact]
	public async Task WriteExplanationFileToBlockedFolder()
	{
		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		A.CallTo(() =>
				fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\blocked\01_MyTask.blocked.md", A<string>.Ignored)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTaskNameInExplanation()
	{
		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("01_MyTask.md");
	}

	[Fact]
	public async Task IncludeExitCodeInExplanation()
	{
		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Exit Code: 1");
	}

	[Fact]
	public async Task IncludeClaudeOutputInExplanation()
	{
		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Some output from Claude");
	}

	[Fact]
	public async Task IncludeErrorOutputInExplanation()
	{
		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Some error occurred");
	}

	[Fact]
	public async Task IncludeTimestampInExplanation()
	{
		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		var today = DateTime.Now.ToString("yyyy-MM-dd");

		writtenContent.Should().Contain(today);
	}

	[Fact]
	public async Task IncludeRecommendedFixForMissingResource()
	{
		processResult.OutputLines = new List<string> { "BLOCKED: required file does not exist" };

		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Resolve the missing resource referenced by the task, then re-queue");
	}

	[Fact]
	public async Task IncludeRecommendedFixForContradictoryInstructions()
	{
		processResult.OutputLines = new List<string> { "BLOCKED: contradictory instructions" };

		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Clarify the task instructions to remove ambiguity, then re-queue");
	}

	[Fact]
	public async Task IncludeGenericRecommendedFixWhenNoPatternMatches()
	{
		processResult.OutputLines = new List<string> { "BLOCKED: Unknown blocker" };
		processResult.ErrorLines = new List<string>();

		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Review the blocker reported by Claude and adjust the task before re-queueing");
	}

	[Fact]
	public async Task UseMarkdownStructureWithHeaders()
	{
		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("# Blocked Task:");
		writtenContent.Should().Contain("## Exit Code");
		writtenContent.Should().Contain("## Claude Output");
		writtenContent.Should().Contain("## Error Output");
		writtenContent.Should().Contain("## Recommended Fix");
	}

	[Fact]
	public async Task LogWhenGeneratingExplanation()
	{
		await generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks\blocked", "01_MyTask.md", processResult);

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("Generating blocked explanation"),
					A<string>.Ignored,
					A<string>.Ignored
				)
			)
			.MustHaveHappened();
	}
}
