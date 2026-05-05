using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class MoveLiveLogTests
{
	private readonly IMoveFile moveFile;
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly MoveLiveLog moveLiveLog;
	private readonly TaskExecutionContext context;
	private readonly TaskExecution task;
	private bool fileExists;

	public MoveLiveLogTests()
	{
		moveFile = A.Fake<IMoveFile>();
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		fileExists = true;

		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).ReturnsLazily(() => fileExists);

		context = new TaskExecutionContext
		{
			Repository = new RepositorySettings { Path = @"C:\Projects\my-api" },
			RepoSettings = new RepoSettings { LogResults = true, Tasks = new TaskSettings() },
			ClaudeSettings = new ClaudeSettings { Model = "claude-opus-4-6" },
			Folders = new TaskFolders
			{
				Todo = @"C:\Projects\my-api\tasks\todo",
				Pending = @"C:\Projects\my-api\tasks\pending",
				Done = @"C:\Projects\my-api\tasks\done",
				Blocked = @"C:\Projects\my-api\tasks\blocked",
				Failed = @"C:\Projects\my-api\tasks\failed",
				Reference = @"C:\Projects\my-api\tasks\reference",
				Logs = @"C:\Projects\my-api\tasks\logs",
			},
			ReferenceFiles = [],
		};

		task = new TaskExecution
		{
			TaskFile = @"C:\Projects\my-api\tasks\todo\05-refactor-run-process.md",
			TaskName = "05-refactor-run-process.md",
			PendingFilePath = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.md",
			Result = new ProcessResult { ExitCode = 0 },
		};

		moveLiveLog = new MoveLiveLog(moveFile, fileSystemTools, logger);
	}

	[Fact]
	public void MoveTheLiveLogFromPendingToTheLogsFolder()
	{
		moveLiveLog.Move(context, task);

		A.CallTo(() =>
				moveFile.Move(
					@"C:\Projects\my-api\tasks\pending\05-refactor-run-process.live.log",
					@"C:\Projects\my-api\tasks\logs\05-refactor-run-process.live.log"
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DoNotMoveWhenTheLiveLogDoesNotExist()
	{
		fileExists = false;

		moveLiveLog.Move(context, task);

		A.CallTo(() => moveFile.Move(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public void LogThatThereWasNoLiveLogToMoveWhenSourceMissing()
	{
		fileExists = false;

		moveLiveLog.Move(context, task);

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("No live log"),
					A<string>.That.Contains("05-refactor-run-process.md")
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void LogThatTheLiveLogIsBeingMovedWhenSourceExists()
	{
		moveLiveLog.Move(context, task);

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("Moving live log"),
					A<string>.That.Contains("05-refactor-run-process.md"),
					A<string>.That.Contains(@"logs\05-refactor-run-process.live.log")
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DeriveTheBaseFileNameFromTheTaskName()
	{
		task.TaskName = "08-relocate-task-logs.md";

		moveLiveLog.Move(context, task);

		A.CallTo(() =>
				moveFile.Move(
					A<string>.That.Contains("08-relocate-task-logs.live.log"),
					A<string>.That.Contains("08-relocate-task-logs.live.log")
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UseTheConfiguredLogsFolderFromTheContext()
	{
		context.Folders.Logs = @"D:\custom\logs";

		moveLiveLog.Move(context, task);

		A.CallTo(() => moveFile.Move(A<string>._, @"D:\custom\logs\05-refactor-run-process.live.log"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void EnsureTheLogsDirectoryExistsBeforeMoving()
	{
		moveLiveLog.Move(context, task);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\logs")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DoNotEnsureDirectoryWhenTheLiveLogDoesNotExist()
	{
		fileExists = false;

		moveLiveLog.Move(context, task);

		A.CallTo(() => fileSystemTools.EnsureDirectory(A<string>._)).MustNotHaveHappened();
	}
}
