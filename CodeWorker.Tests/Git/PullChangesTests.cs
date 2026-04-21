using FatCat.CodeWorker.Git;
using FatCat.CodeWorker.Process;
using Serilog;

namespace Testing.FatCat.CodeWorker.Git;

public class PullChangesTests
{
	private readonly IRunProcess runProcess;
	private readonly ILogger logger;
	private readonly PullChanges pullChanges;
	private readonly string workingDirectory = @"C:\Projects\my-api";
	private ProcessResult processResult;
	private ProcessSettings capturedSettings;

	public PullChangesTests()
	{
		runProcess = A.Fake<IRunProcess>();
		logger = A.Fake<ILogger>();

		processResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "Already up to date." },
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

		pullChanges = new PullChanges(runProcess, logger);
	}

	[Fact]
	public async Task RunGitPullWithCorrectWorkingDirectory()
	{
		await pullChanges.Pull(workingDirectory);

		capturedSettings.WorkingDirectory.Should().Be(workingDirectory);
	}

	[Fact]
	public async Task RunGitPullWithPullArguments()
	{
		await pullChanges.Pull(workingDirectory);

		capturedSettings.FileName.Should().Be("git");
		capturedSettings.Arguments.Should().Be("pull");
	}

	[Fact]
	public async Task ReturnThePullProcessResult()
	{
		var result = await pullChanges.Pull(workingDirectory);

		result.Should().BeSameAs(processResult);
	}

	[Fact]
	public async Task LogWhenPullIsStarting()
	{
		await pullChanges.Pull(workingDirectory);

		A.CallTo(() => logger.Information("Pulling latest changes in {WorkingDirectory}", workingDirectory))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWarningWhenPullFails()
	{
		processResult.ExitCode = 1;

		await pullChanges.Pull(workingDirectory);

		A.CallTo(() => logger.Warning("Git pull failed with exit code {ExitCode}", 1)).MustHaveHappenedOnceExactly();
	}
}
