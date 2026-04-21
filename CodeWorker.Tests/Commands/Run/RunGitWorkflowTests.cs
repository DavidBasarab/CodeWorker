using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Git;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class RunGitWorkflowTests
{
	private readonly ICommitChanges commitChanges;
	private readonly IPushChanges pushChanges;
	private readonly ILogTaskResult logTaskResult;
	private readonly ILogger logger;
	private readonly RunGitWorkflow runGitWorkflow;
	private readonly TaskExecutionContext context;
	private readonly TaskExecution task;
	private ProcessResult commitResult;
	private ProcessResult pushResult;

	public RunGitWorkflowTests()
	{
		commitChanges = A.Fake<ICommitChanges>();
		pushChanges = A.Fake<IPushChanges>();
		logTaskResult = A.Fake<ILogTaskResult>();
		logger = A.Fake<ILogger>();

		commitResult = new ProcessResult { ExitCode = 0 };
		pushResult = new ProcessResult { ExitCode = 0 };

		context = new TaskExecutionContext
		{
			Repository = new RepositorySettings { Path = @"C:\Projects\my-api" },
			RepoSettings = new RepoSettings
			{
				LogResults = true,
				Git = new GitSettings
				{
					CommitAfterEachTask = true,
					PushAfterEachTask = true,
					CommitMessagePrefix = "🤖",
				},
			},
			ReferenceFiles = new List<ReferenceFile>(),
		};

		task = new TaskExecution { TaskFile = @"C:\Projects\my-api\tasks\todo\01_MyTask.md", TaskName = "01_MyTask.md" };

		A.CallTo(() => commitChanges.Commit(A<string>._, A<string>._)).ReturnsLazily(() => Task.FromResult(commitResult));
		A.CallTo(() => pushChanges.Push(A<string>._)).ReturnsLazily(() => Task.FromResult(pushResult));
		A.CallTo(() => logTaskResult.Log(A<string>._, A<string>._, A<ProcessResult>._, A<List<ReferenceFile>>._))
			.Returns(Task.CompletedTask);

		runGitWorkflow = new RunGitWorkflow(commitChanges, pushChanges, logTaskResult, logger);
	}

	[Fact]
	public async Task CommitWhenCommitAfterEachTaskIsTrue()
	{
		await runGitWorkflow.Run(context, task);

		A.CallTo(() => commitChanges.Commit(@"C:\Projects\my-api", A<string>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseCorrectCommitMessageFormat()
	{
		await runGitWorkflow.Run(context, task);

		A.CallTo(() => commitChanges.Commit(A<string>._, "🤖 01_MyTask")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotCommitWhenCommitAfterEachTaskIsFalse()
	{
		context.RepoSettings.Git.CommitAfterEachTask = false;

		await runGitWorkflow.Run(context, task);

		A.CallTo(() => commitChanges.Commit(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotCommitWhenGitSettingsIsNull()
	{
		context.RepoSettings.Git = null;

		await runGitWorkflow.Run(context, task);

		A.CallTo(() => commitChanges.Commit(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task LogCommitResultWhenLogResultsIsEnabled()
	{
		await runGitWorkflow.Run(context, task);

		A.CallTo(() => logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", commitResult, A<List<ReferenceFile>>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnStopWhenCommitFails()
	{
		commitResult = new ProcessResult { ExitCode = 1 };

		var decision = await runGitWorkflow.Run(context, task);

		decision.Should().Be(TaskProcessingDecision.Stop);
	}

	[Fact]
	public async Task NotPushWhenCommitFails()
	{
		commitResult = new ProcessResult { ExitCode = 1 };

		await runGitWorkflow.Run(context, task);

		A.CallTo(() => pushChanges.Push(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task PushWhenPushAfterEachTaskIsTrue()
	{
		await runGitWorkflow.Run(context, task);

		A.CallTo(() => pushChanges.Push(@"C:\Projects\my-api")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotPushWhenPushAfterEachTaskIsFalse()
	{
		context.RepoSettings.Git.PushAfterEachTask = false;

		await runGitWorkflow.Run(context, task);

		A.CallTo(() => pushChanges.Push(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotPushWhenGitSettingsIsNull()
	{
		context.RepoSettings.Git = null;

		await runGitWorkflow.Run(context, task);

		A.CallTo(() => pushChanges.Push(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnStopWhenPushFails()
	{
		pushResult = new ProcessResult { ExitCode = 1 };

		var decision = await runGitWorkflow.Run(context, task);

		decision.Should().Be(TaskProcessingDecision.Stop);
	}

	[Fact]
	public async Task ReturnContinueWhenCommitAndPushSucceed()
	{
		var decision = await runGitWorkflow.Run(context, task);

		decision.Should().Be(TaskProcessingDecision.Continue);
	}

	[Fact]
	public async Task SkipCommitLogWhenLogResultsIsDisabled()
	{
		context.RepoSettings.LogResults = false;

		await runGitWorkflow.Run(context, task);

		A.CallTo(() => logTaskResult.Log(A<string>._, A<string>._, A<ProcessResult>._, A<List<ReferenceFile>>._))
			.MustNotHaveHappened();
	}
}
