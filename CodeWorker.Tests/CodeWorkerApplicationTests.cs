using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Process;
using Serilog;

namespace Testing.FatCat.CodeWorker;

public class CodeWorkerApplicationTests
{
	private readonly IRunClaude runClaude;
	private readonly ILogger logger;
	private readonly CodeWorkerApplication application;
	private readonly string markdownFilePath = @"C:\Tasks\my-task.md";
	private string capturedFilePath;

	public CodeWorkerApplicationTests()
	{
		runClaude = A.Fake<IRunClaude>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => runClaude.Run(A<string>.Ignored))
			.ReturnsLazily(
				(string filePath) =>
				{
					capturedFilePath = filePath;

					return Task.FromResult(new ProcessResult { ExitCode = 0 });
				}
			);

		application = new CodeWorkerApplication(runClaude, logger);
	}

	[Fact]
	public async Task RunClaudeWithTheMarkdownFilePath()
	{
		await application.DoWork(new[] { markdownFilePath });

		capturedFilePath.Should().Be(markdownFilePath);
	}

	[Fact]
	public async Task LogWelcomeMessage()
	{
		await application.DoWork(new[] { markdownFilePath });

		A.CallTo(() => logger.Information("Welcome to Code Worker")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogErrorWhenNoArgumentsProvided()
	{
		await application.DoWork(Array.Empty<string>());

		A.CallTo(() => logger.Error("No markdown file path provided. Usage: CodeWorker <path-to-markdown-file>"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotRunClaudeWhenNoArgumentsProvided()
	{
		await application.DoWork(Array.Empty<string>());

		A.CallTo(() => runClaude.Run(A<string>.Ignored)).MustNotHaveHappened();
	}
}
