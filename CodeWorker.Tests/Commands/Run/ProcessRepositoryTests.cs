using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class ProcessRepositoryTests
{
	private readonly IValidateRepository validateRepository;
	private readonly ILoadRepoSettings loadRepoSettings;
	private readonly IBuildTaskFolders buildTaskFolders;
	private readonly ICollectReferenceFiles collectReferenceFiles;
	private readonly IDiscoverTasks discoverTasks;
	private readonly IProcessTask processTask;
	private readonly ILogger logger;
	private readonly ProcessRepository processRepository;
	private readonly RepositorySettings repositorySettings;
	private readonly ClaudeSettings globalClaudeSettings;
	private readonly TaskFolders taskFolders;
	private RepoSettings repoSettings;
	private RepositoryValidationResult validationResult;
	private TaskProcessingDecision nextDecision;
	private List<ReferenceFile> referenceFiles;

	public ProcessRepositoryTests()
	{
		validateRepository = A.Fake<IValidateRepository>();
		loadRepoSettings = A.Fake<ILoadRepoSettings>();
		buildTaskFolders = A.Fake<IBuildTaskFolders>();
		collectReferenceFiles = A.Fake<ICollectReferenceFiles>();
		discoverTasks = A.Fake<IDiscoverTasks>();
		processTask = A.Fake<IProcessTask>();
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

		repoSettings = new RepoSettings
		{
			Enabled = true,
			LogResults = true,
			Git = new GitSettings(),
			Tasks = new TaskSettings
			{
				TodoFolder = "tasks/todo",
				DoneFolder = "tasks/done",
				PendingFolder = "tasks/pending",
				BlockedFolder = "tasks/blocked",
				FailedFolder = "tasks/failed",
				ReferenceFolder = "tasks/reference",
			},
		};

		taskFolders = new TaskFolders
		{
			Todo = @"C:\Projects\my-api\tasks\todo",
			Pending = @"C:\Projects\my-api\tasks\pending",
			Done = @"C:\Projects\my-api\tasks\done",
			Blocked = @"C:\Projects\my-api\tasks\blocked",
			Failed = @"C:\Projects\my-api\tasks\failed",
			Reference = @"C:\Projects\my-api\tasks\reference",
		};

		referenceFiles = new List<ReferenceFile>();
		nextDecision = TaskProcessingDecision.Continue;

		A.CallTo(() => validateRepository.Validate(A<RepositorySettings>._)).ReturnsLazily(() => validationResult);
		A.CallTo(() => loadRepoSettings.Load(A<string>._)).ReturnsLazily(() => Task.FromResult(repoSettings));
		A.CallTo(() => buildTaskFolders.Build(A<string>._, A<TaskSettings>._)).Returns(taskFolders);
		A.CallTo(() => collectReferenceFiles.Collect(A<string>._)).ReturnsLazily(() => Task.FromResult(referenceFiles));
		A.CallTo(() => discoverTasks.Discover(A<string>._)).Returns(new List<string>());
		A.CallTo(() => processTask.Run(A<TaskExecutionContext>._, A<string>._))
			.ReturnsLazily(() => Task.FromResult(nextDecision));

		processRepository = new ProcessRepository(
			validateRepository,
			loadRepoSettings,
			buildTaskFolders,
			collectReferenceFiles,
			discoverTasks,
			processTask,
			logger
		);
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
			Errors = new List<string> { "Directory not found" },
		};

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => loadRepoSettings.Load(A<string>._)).MustNotHaveHappened();
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
	public async Task BuildTaskFoldersFromRepoSettings()
	{
		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => buildTaskFolders.Build(@"C:\Projects\my-api", repoSettings.Tasks)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CollectReferenceFilesFromReferenceFolder()
	{
		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => collectReferenceFiles.Collect(taskFolders.Reference)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DiscoverTasksInTodoFolder()
	{
		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => discoverTasks.Discover(taskFolders.Todo)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessEachDiscoveredTask()
	{
		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => processTask.Run(A<TaskExecutionContext>._, @"C:\Projects\my-api\tasks\todo\01_First.md"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => processTask.Run(A<TaskExecutionContext>._, @"C:\Projects\my-api\tasks\todo\02_Second.md"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopProcessingWhenDecisionIsStop()
	{
		nextDecision = TaskProcessingDecision.Stop;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(
				new List<string> { @"C:\Projects\my-api\tasks\todo\01_First.md", @"C:\Projects\my-api\tasks\todo\02_Second.md" }
			);

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => processTask.Run(A<TaskExecutionContext>._, A<string>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassMergedClaudeSettingsInContext()
	{
		repoSettings.Claude = new ClaudeSettings { Model = "claude-sonnet-4-6" };

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() =>
				processTask.Run(
					A<TaskExecutionContext>.That.Matches(c =>
						c.ClaudeSettings.Model == "claude-sonnet-4-6" && c.ClaudeSettings.MaxTurns == 10
					),
					A<string>._
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseGlobalClaudeSettingsWhenRepoClaudeIsNull()
	{
		repoSettings.Claude = null;

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() =>
				processTask.Run(
					A<TaskExecutionContext>.That.Matches(c => c.ClaudeSettings.Model == "claude-opus-4-6"),
					A<string>._
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassReferenceFilesInContext()
	{
		referenceFiles = new List<ReferenceFile>
		{
			new() { Name = "context.md", Content = "context content" },
		};

		A.CallTo(() => discoverTasks.Discover(A<string>._))
			.Returns(new List<string> { @"C:\Projects\my-api\tasks\todo\01_MyTask.md" });

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => processTask.Run(A<TaskExecutionContext>.That.Matches(c => c.ReferenceFiles.Count == 1), A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogReferenceFileNamesWhenFilesArePresent()
	{
		referenceFiles = new List<ReferenceFile>
		{
			new() { Name = "context.md", Content = "content" },
			new() { Name = "schema.md", Content = "schema" },
		};

		await processRepository.Process(repositorySettings, globalClaudeSettings);

		A.CallTo(() => logger.Information(A<string>.That.Contains("reference"), A<int>._, A<string>._)).MustHaveHappened();
	}
}
