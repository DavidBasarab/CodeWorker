using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Git;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class ProcessRepositoryTests
{
	private readonly IValidateRepository validateRepository;
	private readonly ILoadRepoSettings loadRepoSettings;
	private readonly IDiscoverTasks discoverTasks;
	private readonly IMoveTask moveTask;
	private readonly IRunClaude runClaude;
	private readonly ILogTaskResult logTaskResult;
	private readonly ICommitChanges commitChanges;
	private readonly IPushChanges pushChanges;
	private readonly IGenerateBlockedExplanation generateBlockedExplanation;
	private readonly ILogger logger;
	private readonly ProcessRepository processRepository;
	private readonly RepositorySettings repositorySettings;
	private RepoSettings repoSettings;
	private ProcessResult claudeResult;
	private ProcessResult commitResult;
	private ProcessResult pushResult;
	private RepositoryValidationResult validationResult;

	public ProcessRepositoryTests()
	{
		validateRepository = A.Fake<IValidateRepository>();
		loadRepoSettings = A.Fake<ILoadRepoSettings>();
		discoverTasks = A.Fake<IDiscoverTasks>();
		moveTask = A.Fake<IMoveTask>();
		runClaude = A.Fake<IRunClaude>();
		logTaskResult = A.Fake<ILogTaskResult>();
		generateBlockedExplanation = A.Fake<IGenerateBlockedExplanation>();
		commitChanges = A.Fake<ICommitChanges>();
		pushChanges = A.Fake<IPushChanges>();
		logger = A.Fake<ILogger>();

		repositorySettings = new RepositorySettings { Path = @"C:\Projects\my-api", Enabled = true };

		validationResult = new RepositoryValidationResult { IsValid = true };

		A.CallTo(() => validateRepository.Validate(A<RepositorySettings>.Ignored)).ReturnsLazily(() => validationResult);

		repoSettings = new RepoSettings
		{
			Enabled = true,
			LogResults = true,
			Git = new GitSettings
			{
				CommitAfterEachTask = true,
				PushAfterEachTask = true,
				CommitMessagePrefix = "🤖",
			},
			Tasks = new TaskSettings
			{
				TodoFolder = "tasks/todo",
				DoneFolder = "tasks/done",
				PendingFolder = "tasks/pending",
				BlockedFolder = "tasks/blocked",
				StopOnBlocked = true,
			},
		};

		claudeResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "Done" },
			ErrorLines = new List<string>(),
		};

		commitResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string>(),
			ErrorLines = new List<string>(),
		};

		pushResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string>(),
			ErrorLines = new List<string>(),
		};

		A.CallTo(() => loadRepoSettings.Load(A<string>.Ignored)).Returns(Task.FromResult(repoSettings));
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored)).Returns(new List<string>());
		A.CallTo(() => runClaude.Run(A<string>.Ignored)).Returns(Task.FromResult(claudeResult));
		A.CallTo(() => logTaskResult.Log(A<string>.Ignored, A<string>.Ignored, A<ProcessResult>.Ignored))
			.Returns(Task.CompletedTask);
		A.CallTo(() => generateBlockedExplanation.Generate(A<string>.Ignored, A<string>.Ignored, A<ProcessResult>.Ignored))
			.Returns(Task.CompletedTask);
		A.CallTo(() => commitChanges.Commit(A<string>.Ignored, A<string>.Ignored))
			.ReturnsLazily(() => Task.FromResult(commitResult));
		A.CallTo(() => pushChanges.Push(A<string>.Ignored)).ReturnsLazily(() => Task.FromResult(pushResult));

		processRepository = new ProcessRepository(
			validateRepository,
			loadRepoSettings,
			discoverTasks,
			moveTask,
			runClaude,
			logTaskResult,
			generateBlockedExplanation,
			commitChanges,
			pushChanges,
			logger
		);
	}

	[Fact]
	public async Task LoadRepoSettingsFromRepositoryPath()
	{
		await processRepository.Process(repositorySettings);

		A.CallTo(() => loadRepoSettings.Load(@"C:\Projects\my-api")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipDisabledRepository()
	{
		repoSettings.Enabled = false;

		await processRepository.Process(repositorySettings);

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task DiscoverTasksInTodoFolder()
	{
		await processRepository.Process(repositorySettings);

		A.CallTo(() => discoverTasks.Discover(@"C:\Projects\my-api\tasks/todo")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveTaskFromTodoToPending()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => moveTask.Move(@"C:\Projects\my-api\tasks\todo\01_MyTask.md", @"C:\Projects\my-api\tasks/pending"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RunClaudeWithPendingFilePath()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => runClaude.Run(@"C:\Projects\my-api\tasks/pending\01_MyTask.md")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogResultWhenLogResultsIsEnabled()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", claudeResult)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipLoggingResultWhenLogResultsIsDisabled()
	{
		repoSettings.LogResults = false;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => logTaskResult.Log(A<string>.Ignored, A<string>.Ignored, A<ProcessResult>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task MoveTaskToDoneOnSuccess()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => moveTask.Move(@"C:\Projects\my-api\tasks/pending\01_MyTask.md", @"C:\Projects\my-api\tasks/done"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveTaskToBlockedOnFailure()
	{
		claudeResult.ExitCode = 1;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => moveTask.Move(@"C:\Projects\my-api\tasks/pending\01_MyTask.md", @"C:\Projects\my-api\tasks/blocked"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopProcessingWhenStopOnBlockedIsTrueAndTaskFails()
	{
		claudeResult.ExitCode = 1;
		repoSettings.Tasks.StopOnBlocked = true;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings);

		A.CallTo(() => runClaude.Run(A<string>.Ignored)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ContinueProcessingWhenStopOnBlockedIsFalseAndTaskFails()
	{
		claudeResult.ExitCode = 1;
		repoSettings.Tasks.StopOnBlocked = false;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings);

		A.CallTo(() => runClaude.Run(A<string>.Ignored)).MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task ProcessMultipleTasksInOrder()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings);

		A.CallTo(() => runClaude.Run(A<string>.That.Contains("01_First.md"))).MustHaveHappenedOnceExactly();
		A.CallTo(() => runClaude.Run(A<string>.That.Contains("02_Second.md"))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWhenTaskIsStarting()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => logger.Information(A<string>.That.Contains("Starting task"), A<string>.That.Contains("01_MyTask.md")))
			.MustHaveHappened();
	}

	[Fact]
	public async Task CommitAfterSuccessfulTaskWhenConfigured()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => commitChanges.Commit(@"C:\Projects\my-api", A<string>.Ignored)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseCorrectCommitMessageFormat()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => commitChanges.Commit(A<string>.Ignored, "🤖 01_MyTask")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotCommitWhenCommitAfterEachTaskIsFalse()
	{
		repoSettings.Git.CommitAfterEachTask = false;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => commitChanges.Commit(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotCommitWhenTaskIsBlocked()
	{
		claudeResult.ExitCode = 1;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => commitChanges.Commit(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task StopRepositoryProcessingWhenCommitFails()
	{
		commitResult.ExitCode = 1;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings);

		A.CallTo(() => runClaude.Run(A<string>.Ignored)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PushAfterSuccessfulTaskWhenConfigured()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => pushChanges.Push(@"C:\Projects\my-api")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotPushWhenPushAfterEachTaskIsFalse()
	{
		repoSettings.Git.PushAfterEachTask = false;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => pushChanges.Push(A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotPushWhenTaskIsBlocked()
	{
		claudeResult.ExitCode = 1;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => pushChanges.Push(A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task StopRepositoryProcessingWhenPushFails()
	{
		pushResult.ExitCode = 1;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings);

		A.CallTo(() => runClaude.Run(A<string>.Ignored)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotCommitWhenGitSettingsIsNull()
	{
		repoSettings.Git = null;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => commitChanges.Commit(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotPushWhenGitSettingsIsNull()
	{
		repoSettings.Git = null;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => pushChanges.Push(A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ValidateRepositoryBeforeProcessing()
	{
		await processRepository.Process(repositorySettings);

		A.CallTo(() => validateRepository.Validate(repositorySettings)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipRepositoryWhenValidationFails()
	{
		validationResult = new RepositoryValidationResult
		{
			IsValid = false,
			Errors = new List<string> { "Directory not found: C:\\Projects\\my-api\\tasks" },
		};

		await processRepository.Process(repositorySettings);

		A.CallTo(() => loadRepoSettings.Load(A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotDiscoverTasksWhenValidationFails()
	{
		validationResult = new RepositoryValidationResult
		{
			IsValid = false,
			Errors = new List<string> { "Directory not found: C:\\Projects\\my-api\\tasks" },
		};

		await processRepository.Process(repositorySettings);

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GenerateBlockedExplanationWhenTaskIsBlocked()
	{
		claudeResult.ExitCode = 1;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks/blocked", "01_MyTask.md", claudeResult))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotGenerateBlockedExplanationWhenTaskSucceeds()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => generateBlockedExplanation.Generate(A<string>.Ignored, A<string>.Ignored, A<ProcessResult>.Ignored))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task GenerateBlockedExplanationBeforeStopOnBlocked()
	{
		claudeResult.ExitCode = 1;
		repoSettings.Tasks.StopOnBlocked = true;

		A.CallTo(() => discoverTasks.Discover(A<string>.Ignored))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings);

		A.CallTo(() => generateBlockedExplanation.Generate(A<string>.Ignored, A<string>.Ignored, A<ProcessResult>.Ignored))
			.MustHaveHappenedOnceExactly();
	}
}
