using FatCat.CodeWorker.Git;
using FatCat.CodeWorker.Process;
using Serilog;

namespace Testing.FatCat.CodeWorker.Git;

public class CommitChangesTests
{
	private readonly IRunProcess runProcess;
	private readonly ILogger logger;
	private readonly CommitChanges commitChanges;
	private readonly string workingDirectory = @"C:\Projects\my-api";
	private readonly string commitMessage = "🤖 01-refactor-auth-service";
	private ProcessResult addResult;
	private ProcessResult commitResult;
	private readonly List<ProcessSettings> capturedSettings = new();

	public CommitChangesTests()
	{
		runProcess = A.Fake<IRunProcess>();
		logger = A.Fake<ILogger>();

		addResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string>(),
			ErrorLines = new List<string>(),
		};

		commitResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "[main abc1234] 🤖 01-refactor-auth-service" },
			ErrorLines = new List<string>(),
		};

		A.CallTo(() => runProcess.Run(A<ProcessSettings>.Ignored))
			.ReturnsLazily(
				(ProcessSettings settings) =>
				{
					capturedSettings.Add(settings);

					if (settings.Arguments.StartsWith("add"))
					{
						return Task.FromResult(addResult);
					}

					return Task.FromResult(commitResult);
				}
			);

		commitChanges = new CommitChanges(runProcess, logger);
	}

	[Fact]
	public async Task RunGitAddWithCorrectWorkingDirectory()
	{
		await commitChanges.Commit(workingDirectory, commitMessage);

		capturedSettings[0].WorkingDirectory.Should().Be(workingDirectory);
	}

	[Fact]
	public async Task RunGitAddWithAddAllArguments()
	{
		await commitChanges.Commit(workingDirectory, commitMessage);

		capturedSettings[0].FileName.Should().Be("git");
		capturedSettings[0].Arguments.Should().Be("add -A");
	}

	[Fact]
	public async Task RunGitCommitWithCorrectWorkingDirectory()
	{
		await commitChanges.Commit(workingDirectory, commitMessage);

		capturedSettings[1].WorkingDirectory.Should().Be(workingDirectory);
	}

	[Fact]
	public async Task RunGitCommitWithCorrectMessage()
	{
		await commitChanges.Commit(workingDirectory, commitMessage);

		capturedSettings[1].FileName.Should().Be("git");
		capturedSettings[1].Arguments.Should().Be($"commit -m \"{commitMessage}\"");
	}

	[Fact]
	public async Task ReturnTheCommitProcessResult()
	{
		var result = await commitChanges.Commit(workingDirectory, commitMessage);

		result.Should().BeSameAs(commitResult);
	}

	[Fact]
	public async Task LogWhenCommitIsStarting()
	{
		await commitChanges.Commit(workingDirectory, commitMessage);

		A.CallTo(() =>
				logger.Information(
					"Committing changes in {WorkingDirectory} with message {Message}",
					workingDirectory,
					commitMessage
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWarningWhenGitAddFails()
	{
		addResult.ExitCode = 1;

		await commitChanges.Commit(workingDirectory, commitMessage);

		A.CallTo(() => logger.Warning("Git add failed with exit code {ExitCode}", 1)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotRunCommitWhenGitAddFails()
	{
		addResult.ExitCode = 1;

		await commitChanges.Commit(workingDirectory, commitMessage);

		capturedSettings.Should().HaveCount(1);
	}

	[Fact]
	public async Task ReturnAddResultWhenGitAddFails()
	{
		addResult.ExitCode = 1;

		var result = await commitChanges.Commit(workingDirectory, commitMessage);

		result.Should().BeSameAs(addResult);
	}

	[Fact]
	public async Task LogWarningWhenCommitFails()
	{
		commitResult.ExitCode = 1;

		await commitChanges.Commit(workingDirectory, commitMessage);

		A.CallTo(() => logger.Warning("Git commit failed with exit code {ExitCode}", 1)).MustHaveHappenedOnceExactly();
	}
}
