using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Process;
using Serilog;

namespace Testing.FatCat.CodeWorker.Claude;

public class ClaudeRunnerTests
{
	private readonly IRunProcess runProcess;
	private readonly ILogger logger;
	private readonly ClaudeRunner claudeRunner;
	private readonly string markdownFilePath = @"C:\Tasks\some-task.md";
	private ProcessResult processResult;
	private ProcessSettings capturedSettings;

	public ClaudeRunnerTests()
	{
		runProcess = A.Fake<IRunProcess>();
		logger = A.Fake<ILogger>();

		processResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "Claude output line 1", "Claude output line 2" },
		};

		A.CallTo(() => runProcess.Run(A<ProcessSettings>.Ignored))
			.ReturnsLazily(
				(ProcessSettings settings) =>
				{
					capturedSettings = settings;

					return Task.FromResult(processResult);
				}
			);

		claudeRunner = new ClaudeRunner(runProcess, logger);
	}

	[Fact]
	public async Task PassClaudeAsTheFileName()
	{
		await claudeRunner.Run(markdownFilePath);

		capturedSettings.FileName.Should().Be("claude");
	}

	[Fact]
	public async Task PassPrintFlagWithInputFile()
	{
		await claudeRunner.Run(markdownFilePath);

		capturedSettings.Arguments.Should().Be($"-p --input-file \"{markdownFilePath}\"");
	}

	[Fact]
	public async Task ReturnTheProcessResult()
	{
		var result = await claudeRunner.Run(markdownFilePath);

		result.Should().BeSameAs(processResult);
	}

	[Fact]
	public async Task LogTheStartOfTheClaudeRun()
	{
		await claudeRunner.Run(markdownFilePath);

		A.CallTo(() => logger.Information("Starting Claude with markdown file {MarkdownFilePath}", markdownFilePath))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogTheExitCode()
	{
		processResult.ExitCode = 42;

		await claudeRunner.Run(markdownFilePath);

		A.CallTo(() => logger.Information("Claude exited with code {ExitCode}", 42)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWarningWhenExitCodeIsNotZero()
	{
		processResult.ExitCode = 1;

		await claudeRunner.Run(markdownFilePath);

		A.CallTo(() => logger.Warning("Claude exited with non-zero exit code {ExitCode}", 1)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotLogWarningWhenExitCodeIsZero()
	{
		processResult.ExitCode = 0;

		await claudeRunner.Run(markdownFilePath);

		A.CallTo(() => logger.Warning(A<string>.Ignored, A<int>.Ignored)).MustNotHaveHappened();
	}
}
