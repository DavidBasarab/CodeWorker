using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Process;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class RunCommandTests
{
	private readonly IRunClaude runClaude;
	private readonly ILogger logger;
	private readonly RunCommand command;
	private readonly string markdownFilePath = @"C:\Tasks\my-task.md";
	private string capturedFilePath;

	public RunCommandTests()
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

		command = new RunCommand(runClaude, logger);
	}

	[Fact]
	public async Task PassFilePathToRunClaude()
	{
		await command.Execute(new[] { markdownFilePath });

		capturedFilePath.Should().Be(markdownFilePath);
	}

	[Fact]
	public async Task LogTheMarkdownFilePath()
	{
		await command.Execute(new[] { markdownFilePath });

		A.CallTo(() => logger.Information("Running task from {MarkdownFilePath}", markdownFilePath))
			.MustHaveHappenedOnceExactly();
	}
}
