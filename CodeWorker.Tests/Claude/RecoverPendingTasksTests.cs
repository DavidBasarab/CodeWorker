using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Claude;

public class RecoverPendingTasksTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ITranscriptPaths transcriptPaths;
	private readonly IClassifyTaskResult classifyTaskResult;
	private readonly IMoveTask moveTask;
	private readonly ILogger logger;
	private readonly RecoverPendingTasks recover;

	private readonly RepositorySettings repository;
	private readonly RepoSettings repoSettings;
	private readonly TaskFolders folders;

	private readonly string pendingTaskPath = @"C:\Projects\my-api\tasks\pending\01-task.md";
	private readonly TranscriptPaths paths;

	private TaskOutcome classification = TaskOutcome.Done;
	private List<string> pendingFiles;
	private List<string> transcriptLines;
	private bool sentinelExists;
	private bool transcriptExists;

	public RecoverPendingTasksTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		transcriptPaths = A.Fake<ITranscriptPaths>();
		classifyTaskResult = A.Fake<IClassifyTaskResult>();
		moveTask = A.Fake<IMoveTask>();
		logger = A.Fake<ILogger>();

		repository = new RepositorySettings { Path = @"C:\Projects\my-api" };
		repoSettings = new RepoSettings();

		folders = new TaskFolders
		{
			Todo = @"C:\Projects\my-api\tasks\todo",
			Pending = @"C:\Projects\my-api\tasks\pending",
			Done = @"C:\Projects\my-api\tasks\done",
			Blocked = @"C:\Projects\my-api\tasks\blocked",
			Failed = @"C:\Projects\my-api\tasks\failed",
			Reference = @"C:\Projects\my-api\tasks\reference",
			Logs = @"C:\Projects\my-api\tasks\logs",
		};

		paths = new TranscriptPaths
		{
			TaskName = "01-task.md",
			PromptFile = @"C:\Projects\my-api\tasks\pending\01-task.prompt.txt",
			TranscriptFile = @"C:\Projects\my-api\tasks\pending\01-task.transcript.jsonl",
			StderrFile = @"C:\Projects\my-api\tasks\pending\01-task.stderr.log",
			DoneSentinel = @"C:\Projects\my-api\tasks\pending\01-task.done",
			PidFile = @"C:\Projects\my-api\tasks\pending\01-task.wrapper.pid",
			LiveLogFile = @"C:\Projects\my-api\tasks\pending\01-task.live.log",
		};

		pendingFiles = new List<string> { pendingTaskPath };
		transcriptLines = new List<string> { "{\"type\":\"orchestrator-done\",\"exitCode\":0}" };
		sentinelExists = true;
		transcriptExists = true;

		A.CallTo(() => fileSystemTools.DirectoryExists(folders.Pending)).Returns(true);
		A.CallTo(() => fileSystemTools.GetFiles(folders.Pending)).ReturnsLazily(() => pendingFiles);
		A.CallTo(() => transcriptPaths.For(pendingTaskPath)).Returns(paths);
		A.CallTo(() => fileSystemTools.FileExists(paths.DoneSentinel)).ReturnsLazily(() => sentinelExists);
		A.CallTo(() => fileSystemTools.FileExists(paths.TranscriptFile)).ReturnsLazily(() => transcriptExists);
		A.CallTo(() => fileSystemTools.FileExists(paths.StderrFile)).Returns(false);
		A.CallTo(() => fileSystemTools.FileExists(paths.LiveLogFile)).Returns(false);
		A.CallTo(() => fileSystemTools.ReadAllLines(paths.TranscriptFile))
			.ReturnsLazily(() => Task.FromResult(transcriptLines));
		A.CallTo(() => classifyTaskResult.Classify(A<global::FatCat.CodeWorker.Process.ProcessResult>._))
			.ReturnsLazily(() => classification);

		recover = new RecoverPendingTasks(fileSystemTools, transcriptPaths, classifyTaskResult, moveTask, logger);
	}

	[Fact]
	public async Task DoNothingWhenPendingDirectoryDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(folders.Pending)).Returns(false);

		await recover.Recover(repository, repoSettings, folders);

		A.CallTo(() => moveTask.Move(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task SkipPendingTasksWithoutDoneSentinel()
	{
		sentinelExists = false;

		await recover.Recover(repository, repoSettings, folders);

		A.CallTo(() => moveTask.Move(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task MoveTaskToDoneWhenClassifiedAsDone()
	{
		classification = TaskOutcome.Done;

		await recover.Recover(repository, repoSettings, folders);

		A.CallTo(() => moveTask.Move(pendingTaskPath, folders.Done)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveTaskToBlockedWhenClassifiedAsBlocked()
	{
		classification = TaskOutcome.Blocked;

		await recover.Recover(repository, repoSettings, folders);

		A.CallTo(() => moveTask.Move(pendingTaskPath, folders.Blocked)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveTaskToFailedWhenClassifiedAsFailed()
	{
		classification = TaskOutcome.Failed;

		await recover.Recover(repository, repoSettings, folders);

		A.CallTo(() => moveTask.Move(pendingTaskPath, folders.Failed)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ClassifyUsesParsedResultEventFromTranscript()
	{
		transcriptLines = new List<string>
		{
			"{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false}",
			"{\"type\":\"orchestrator-done\",\"exitCode\":0}",
		};

		await recover.Recover(repository, repoSettings, folders);

		A.CallTo(() =>
				classifyTaskResult.Classify(
					A<global::FatCat.CodeWorker.Process.ProcessResult>.That.Matches(r =>
						r.ResultEvent != null && r.ResultEvent.Subtype == "success"
					)
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteTheDoneSentinelAfterRecovery()
	{
		await recover.Recover(repository, repoSettings, folders);

		A.CallTo(() => fileSystemTools.DeleteFile(paths.DoneSentinel)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipNonMarkdownFilesInPending()
	{
		pendingFiles = new List<string>
		{
			@"C:\Projects\my-api\tasks\pending\01-task.transcript.jsonl",
			@"C:\Projects\my-api\tasks\pending\01-task.done",
		};

		await recover.Recover(repository, repoSettings, folders);

		A.CallTo(() => moveTask.Move(A<string>._, A<string>._)).MustNotHaveHappened();
	}
}
