using FatCat.CodeWorker.Git;
using FatCat.CodeWorker.Process;
using Serilog;

namespace Testing.FatCat.CodeWorker.Git;

public class PushChangesTests
{
	private readonly IRunProcess runProcess;
	private readonly ILogger logger;
	private readonly PushChanges pushChanges;
	private readonly string workingDirectory = @"C:\Projects\my-api";
	private ProcessResult processResult;
	private ProcessSettings capturedSettings;

	public PushChangesTests()
	{
		runProcess = A.Fake<IRunProcess>();
		logger = A.Fake<ILogger>();

		processResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string>(),
			ErrorLines = new List<string>(),
		};

		A.CallTo(() => runProcess.Run(A<ProcessSettings>._))
			.ReturnsLazily(
				(ProcessSettings settings) =>
				{
					capturedSettings = settings;

					return Task.FromResult(processResult);
				}
			);

		pushChanges = new PushChanges(runProcess, logger);
	}

	[Fact]
	public async Task RunGitPushWithCorrectWorkingDirectory()
	{
		await pushChanges.Push(workingDirectory);

		capturedSettings.WorkingDirectory.Should().Be(workingDirectory);
	}

	[Fact]
	public async Task RunGitPushWithPushArguments()
	{
		await pushChanges.Push(workingDirectory);

		capturedSettings.FileName.Should().Be("git");
		capturedSettings.Arguments.Should().Be("push");
	}

	[Fact]
	public async Task ReturnThePushProcessResult()
	{
		var result = await pushChanges.Push(workingDirectory);

		result.Should().BeSameAs(processResult);
	}

	[Fact]
	public async Task LogWhenPushIsStarting()
	{
		await pushChanges.Push(workingDirectory);

		A.CallTo(() => logger.Information("Pushing changes in {WorkingDirectory}", workingDirectory))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWarningWhenPushFails()
	{
		processResult.ExitCode = 1;

		await pushChanges.Push(workingDirectory);

		A.CallTo(() => logger.Warning("Git push failed with exit code {ExitCode}", 1)).MustHaveHappenedOnceExactly();
	}
}
