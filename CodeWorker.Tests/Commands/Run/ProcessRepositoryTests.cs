using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Git;
using FatCat.CodeWorker.History;
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
	private readonly IGenerateFailedExplanation generateFailedExplanation;
	private readonly IClassifyTaskResult classifyTaskResult;
	private readonly ICollectReferenceFiles collectReferenceFiles;
	private readonly IRecordRunHistory recordRunHistory;
	private readonly ILogger logger;
	private readonly ProcessRepository processRepository;
	private readonly RepositorySettings repositorySettings;
	private readonly ClaudeSettings globalClaudeSettings;
	private RepoSettings repoSettings;
	private ProcessResult claudeResult;
	private ProcessResult commitResult;
	private ProcessResult pushResult;
	private RepositoryValidationResult validationResult;
	private TaskOutcome currentOutcome;

	public ProcessRepositoryTests()
	{
		validateRepository = A.Fake<IValidateRepository>();
		loadRepoSettings = A.Fake<ILoadRepoSettings>();
		discoverTasks = A.Fake<IDiscoverTasks>();
		moveTask = A.Fake<IMoveTask>();
		runClaude = A.Fake<IRunClaude>();
		logTaskResult = A.Fake<ILogTaskResult>();
		generateBlockedExplanation = A.Fake<IGenerateBlockedExplanation>();
		generateFailedExplanation = A.Fake<IGenerateFailedExplanation>();
		classifyTaskResult = A.Fake<IClassifyTaskResult>();
		collectReferenceFiles = A.Fake<ICollectReferenceFiles>();
		commitChanges = A.Fake<ICommitChanges>();
		pushChanges = A.Fake<IPushChanges>();
		recordRunHistory = A.Fake<IRecordRunHistory>();
		logger = A.Fake<ILogger>();

		repositorySettings = new RepositorySettings { Path = @"C:\Projects\my-api", Enabled = true };

		globalClaudeSettings = new ClaudeSettings
		{
			Model = "claude-opus-4-6",
			MaxTurns = 10,
			SkipPermissions = true,
			OutputFormat = "json",
			TimeoutMinutes = 30,
		};

		validationResult = new RepositoryValidationResult { IsValid = true };

		A.CallTo(() => validateRepository.Validate(A<RepositorySettings>._)).ReturnsLazily(() => validationResult);

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
				FailedFolder = "tasks/failed",
				ReferenceFolder = "tasks/reference",
				StopOnBlocked = true,
				StopOnFailed = true,
			},
		};

		currentOutcome = TaskOutcome.Done;

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

		A.CallTo(() => loadRepoSettings.Load(A<string>._)).Returns(Task.FromResult(repoSettings));
		A.CallTo(() => discoverTasks.Discover(A<string>._)).Returns(new List<string>());
		A.CallTo(() => collectReferenceFiles.Collect(A<string>._)).Returns(Task.FromResult(new List<ReferenceFile>()));
		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._))
			.Returns(Task.FromResult(claudeResult));
		A.CallTo(() => logTaskResult.Log(A<string>._, A<string>._, A<ProcessResult>._, A<List<ReferenceFile>>._))
			.Returns(Task.CompletedTask);
		A.CallTo(() => generateBlockedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._))
			.Returns(Task.CompletedTask);
		A.CallTo(() => generateFailedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._))
			.Returns(Task.CompletedTask);
		A.CallTo(() => classifyTaskResult.Classify(A<ProcessResult>._)).ReturnsLazily(() => currentOutcome);
		A.CallTo(() => commitChanges.Commit(A<string>._, A<string>._)).ReturnsLazily(() => Task.FromResult(commitResult));
		A.CallTo(() => pushChanges.Push(A<string>._)).ReturnsLazily(() => Task.FromResult(pushResult));

		processRepository = new ProcessRepository(
			validateRepository,
			loadRepoSettings,
			discoverTasks,
			moveTask,
			runClaude,
			logTaskResult,
			generateBlockedExplanation,
			generateFailedExplanation,
			classifyTaskResult,
			collectReferenceFiles,
			commitChanges,
			pushChanges,
			recordRunHistory,
			logger
		);
	}

	[Fact]
	public async Task LoadRepoSettingsFromRepositoryPath()
	{
		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => loadRepoSettings.Load(@"C:\Projects\my-api")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipDisabledRepository()
	{
		repoSettings.Enabled = false;

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => discoverTasks.Discover(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task DiscoverTasksInTodoFolder()
	{
		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => discoverTasks.Discover(@"C:\Projects\my-api\tasks/todo")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveTaskFromTodoToPending()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => moveTask.Move(@"C:\Projects\my-api\tasks\todo\01_MyTask.md", @"C:\Projects\my-api\tasks/pending"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RunClaudeWithPendingFilePath()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() =>
				runClaude.Run(@"C:\Projects\my-api\tasks/pending\01_MyTask.md", A<ClaudeSettings>._, A<List<ReferenceFile>>._)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogResultWhenLogResultsIsEnabled()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", claudeResult, A<List<ReferenceFile>>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipLoggingResultWhenLogResultsIsDisabled()
	{
		repoSettings.LogResults = false;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => logTaskResult.Log(A<string>._, A<string>._, A<ProcessResult>._, A<List<ReferenceFile>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RecordSuccessInRunHistoryWhenOutcomeIsDone()
	{
		currentOutcome = TaskOutcome.Done;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() =>
				recordRunHistory.Record(
					A<RunHistoryEntry>.That.Matches(entry =>
						entry.Repository == @"C:\Projects\my-api" && entry.TaskName == "01_MyTask.md" && entry.Success == true
					)
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailureInRunHistoryWhenOutcomeIsFailed()
	{
		currentOutcome = TaskOutcome.Failed;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => recordRunHistory.Record(A<RunHistoryEntry>.That.Matches(entry => entry.Success == false)))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveTaskToDoneOnSuccess()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => moveTask.Move(@"C:\Projects\my-api\tasks/pending\01_MyTask.md", @"C:\Projects\my-api\tasks/done"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveTaskToBlockedOnFailure()
	{
		currentOutcome = TaskOutcome.Blocked;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => moveTask.Move(@"C:\Projects\my-api\tasks/pending\01_MyTask.md", @"C:\Projects\my-api\tasks/blocked"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopProcessingWhenStopOnBlockedIsTrueAndTaskFails()
	{
		currentOutcome = TaskOutcome.Blocked;
		repoSettings.Tasks.StopOnBlocked = true;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ContinueProcessingWhenStopOnBlockedIsFalseAndTaskFails()
	{
		currentOutcome = TaskOutcome.Blocked;
		repoSettings.Tasks.StopOnBlocked = false;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._))
			.MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task ProcessMultipleTasksInOrder()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => runClaude.Run(A<string>.That.Contains("01_First.md"), A<ClaudeSettings>._, A<List<ReferenceFile>>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => runClaude.Run(A<string>.That.Contains("02_Second.md"), A<ClaudeSettings>._, A<List<ReferenceFile>>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWhenTaskIsStarting()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => logger.Information(A<string>.That.Contains("Starting task"), A<string>.That.Contains("01_MyTask.md")))
			.MustHaveHappened();
	}

	[Fact]
	public async Task CommitAfterSuccessfulTaskWhenConfigured()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => commitChanges.Commit(@"C:\Projects\my-api", A<string>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseCorrectCommitMessageFormat()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => commitChanges.Commit(A<string>._, "🤖 01_MyTask")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotCommitWhenCommitAfterEachTaskIsFalse()
	{
		repoSettings.Git.CommitAfterEachTask = false;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => commitChanges.Commit(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotCommitWhenTaskIsBlocked()
	{
		currentOutcome = TaskOutcome.Blocked;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => commitChanges.Commit(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task StopRepositoryProcessingWhenCommitFails()
	{
		commitResult.ExitCode = 1;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PushAfterSuccessfulTaskWhenConfigured()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => pushChanges.Push(@"C:\Projects\my-api")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotPushWhenPushAfterEachTaskIsFalse()
	{
		repoSettings.Git.PushAfterEachTask = false;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => pushChanges.Push(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotPushWhenTaskIsBlocked()
	{
		currentOutcome = TaskOutcome.Blocked;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => pushChanges.Push(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task StopRepositoryProcessingWhenPushFails()
	{
		pushResult.ExitCode = 1;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotCommitWhenGitSettingsIsNull()
	{
		repoSettings.Git = null;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => commitChanges.Commit(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotPushWhenGitSettingsIsNull()
	{
		repoSettings.Git = null;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => pushChanges.Push(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ValidateRepositoryBeforeProcessing()
	{
		await processRepository.Process(repositorySettings, globalClaudeSettings);

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

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => loadRepoSettings.Load(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotDiscoverTasksWhenValidationFails()
	{
		validationResult = new RepositoryValidationResult
		{
			IsValid = false,
			Errors = new List<string> { "Directory not found: C:\\Projects\\my-api\\tasks" },
		};

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => discoverTasks.Discover(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GenerateBlockedExplanationWhenTaskIsBlocked()
	{
		currentOutcome = TaskOutcome.Blocked;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => generateBlockedExplanation.Generate(@"C:\Projects\my-api\tasks/blocked", "01_MyTask.md", claudeResult))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotGenerateBlockedExplanationWhenTaskSucceeds()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => generateBlockedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task PassMergedClaudeSettingsToClaudeRunner()
	{
		repoSettings.Claude = new ClaudeSettings { Model = "claude-sonnet-4-6" };

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() =>
				runClaude.Run(
					A<string>._,
					A<ClaudeSettings>.That.Matches(c => c.Model == "claude-sonnet-4-6" && c.MaxTurns == 10),
					A<List<ReferenceFile>>._
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseGlobalSettingsWhenRepoClaudeSettingsIsNull()
	{
		repoSettings.Claude = null;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() =>
				runClaude.Run(
					A<string>._,
					A<ClaudeSettings>.That.Matches(c => c.Model == "claude-opus-4-6" && c.MaxTurns == 10),
					A<List<ReferenceFile>>._
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateBlockedExplanationBeforeStopOnBlocked()
	{
		currentOutcome = TaskOutcome.Blocked;
		repoSettings.Tasks.StopOnBlocked = true;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => generateBlockedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CollectReferenceFilesFromConfiguredFolder()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => collectReferenceFiles.Collect(@"C:\Projects\my-api\tasks/reference")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassReferenceFilesToClaudeRunner()
	{
		var references = new List<ReferenceFile>
		{
			new() { Name = "context.md", Content = "context content" },
		};

		A.CallTo(() => collectReferenceFiles.Collect(A<string>._)).Returns(Task.FromResult(references));

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>.That.Matches(r => r.Count == 1)))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassReferenceFilesToLogTaskResult()
	{
		var references = new List<ReferenceFile>
		{
			new() { Name = "context.md", Content = "context content" },
		};

		A.CallTo(() => collectReferenceFiles.Collect(A<string>._)).Returns(Task.FromResult(references));

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() =>
				logTaskResult.Log(
					A<string>._,
					A<string>._,
					A<ProcessResult>._,
					A<List<ReferenceFile>>.That.Matches(r => r.Count == 1)
				)
			)
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogReferenceFileNames()
	{
		var references = new List<ReferenceFile>
		{
			new() { Name = "context.md", Content = "content" },
			new() { Name = "schema.md", Content = "schema" },
		};

		A.CallTo(() => collectReferenceFiles.Collect(A<string>._)).Returns(Task.FromResult(references));

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => logger.Information(A<string>.That.Contains("reference"), A<int>._, A<string>._)).MustHaveHappened();
	}

	[Fact]
	public async Task MoveTaskToFailedWhenClassificationIsFailed()
	{
		currentOutcome = TaskOutcome.Failed;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => moveTask.Move(@"C:\Projects\my-api\tasks/pending\01_MyTask.md", @"C:\Projects\my-api\tasks/failed"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateFailedExplanationWhenClassificationIsFailed()
	{
		currentOutcome = TaskOutcome.Failed;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => generateFailedExplanation.Generate(@"C:\Projects\my-api\tasks/failed", "01_MyTask.md", claudeResult))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotGenerateFailedExplanationWhenTaskSucceeds()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => generateFailedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotGenerateBlockedExplanationWhenClassificationIsFailed()
	{
		currentOutcome = TaskOutcome.Failed;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => generateBlockedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task StopProcessingWhenStopOnFailedIsTrueAndTaskFails()
	{
		currentOutcome = TaskOutcome.Failed;
		repoSettings.Tasks.StopOnFailed = true;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ContinueProcessingWhenStopOnFailedIsFalseAndTaskFails()
	{
		currentOutcome = TaskOutcome.Failed;
		repoSettings.Tasks.StopOnFailed = false;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._))
			.MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task NotCommitWhenTaskFails()
	{
		currentOutcome = TaskOutcome.Failed;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => commitChanges.Commit(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotPushWhenTaskFails()
	{
		currentOutcome = TaskOutcome.Failed;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => pushChanges.Push(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ClassifyTaskResultAfterRunningClaude()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => classifyTaskResult.Classify(claudeResult)).MustHaveHappenedOnceExactly();
	}
}
