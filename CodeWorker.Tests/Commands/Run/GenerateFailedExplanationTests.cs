using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Process;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class GenerateFailedExplanationTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly GenerateFailedExplanation generateFailedExplanation;
	private ProcessResult processResult;
	private string writtenContent;

	public GenerateFailedExplanationTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		processResult = new ProcessResult
		{
			ExitCode = -1,
			TimedOut = true,
			OutputLines = new List<string> { "Some output from Claude" },
			ErrorLines = new List<string> { "Process timed out after 30 minutes" },
		};

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>.Ignored, A<string>.Ignored))
			.Invokes((string path, string content) => writtenContent = content)
			.Returns(Task.CompletedTask);

		generateFailedExplanation = new GenerateFailedExplanation(fileSystemTools, logger);
	}

	[Fact]
	public async Task WriteExplanationFileToFailedFolder()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\failed\01_MyTask.failed.md", A<string>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTaskNameInExplanation()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("01_MyTask.md");
	}

	[Fact]
	public async Task IncludeExitCodeInExplanation()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Exit Code: -1");
	}

	[Fact]
	public async Task IncludeClaudeOutputInExplanation()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Some output from Claude");
	}

	[Fact]
	public async Task IncludeErrorOutputInExplanation()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Process timed out after 30 minutes");
	}

	[Fact]
	public async Task IncludeTimestampInExplanation()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		var today = DateTime.Now.ToString("yyyy-MM-dd");

		writtenContent.Should().Contain(today);
	}

	[Fact]
	public async Task IncludeInfrastructureFailureSectionWhenTimedOut()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Timed Out");
	}

	[Fact]
	public async Task IncludeInfrastructureFailureSectionWhenFailedToStart()
	{
		processResult.TimedOut = false;
		processResult.FailedToStart = true;

		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Failed To Start");
	}

	[Fact]
	public async Task IncludeRecommendedFixForTimeoutError()
	{
		processResult.TimedOut = true;

		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Increase timeout or break the task into smaller pieces");
	}

	[Fact]
	public async Task IncludeRecommendedFixForFailedToStart()
	{
		processResult.TimedOut = false;
		processResult.FailedToStart = true;
		processResult.ErrorLines = new List<string>
		{
			"Failed to start process claude: The system cannot find the file specified",
		};

		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Verify the Claude CLI is installed and available on PATH");
	}

	[Fact]
	public async Task IncludeRecommendedFixForAuthenticationError()
	{
		processResult.TimedOut = false;
		processResult.ErrorLines = new List<string> { "authentication failed" };

		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Check API key and authentication configuration");
	}

	[Fact]
	public async Task IncludeRecommendedFixForTokenLimitError()
	{
		processResult.TimedOut = false;
		processResult.ErrorLines = new List<string> { "token limit exceeded" };

		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Increase token limit or simplify the task");
	}

	[Fact]
	public async Task IncludeGenericRecommendedFixWhenNoPatternMatches()
	{
		processResult.TimedOut = false;
		processResult.ErrorLines = new List<string> { "Unknown failure" };

		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("Review the error output above and address the root cause");
	}

	[Fact]
	public async Task UseMarkdownStructureWithHeaders()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		writtenContent.Should().Contain("# Failed Task:");
		writtenContent.Should().Contain("## Exit Code");
		writtenContent.Should().Contain("## Claude Output");
		writtenContent.Should().Contain("## Error Output");
		writtenContent.Should().Contain("## Recommended Fix");
	}

	[Fact]
	public async Task LogWhenGeneratingExplanation()
	{
		await generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks\failed", "01_MyTask.md", processResult);

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("Generating failed explanation"),
					A<string>.Ignored,
					A<string>.Ignored
				)
			)
			.MustHaveHappened();
	}
}
