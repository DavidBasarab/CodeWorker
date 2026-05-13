using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class WriteTaskLogTests
{
	private readonly IWriteFile writeFile;
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly WriteTaskLog writeTaskLog;
	private readonly TaskExecutionContext context;
	private readonly TaskExecution task;

	public WriteTaskLogTests()
	{
		writeFile = A.Fake<IWriteFile>();
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => writeFile.Write(A<string>._, A<string>._)).Returns(Task.CompletedTask);

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
			ReferenceFiles = new List<ReferenceFile>
			{
				new() { Name = "CLAUDE.md", Content = "some content" },
				new() { Name = "architecture.md", Content = "other content" },
			},
		};

		task = new TaskExecution
		{
			TaskFile = @"C:\Projects\my-api\tasks\todo\05-refactor-run-process.md",
			TaskName = "05-refactor-run-process.md",
			PendingFilePath = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.md",
			Result = new ProcessResult
			{
				ExitCode = 0,
				OutputLines = new List<string> { "Line one", "Line two" },
				ErrorLines = new List<string> { "Error one" },
				TimedOut = false,
				FailedToStart = false,
			},
		};

		writeTaskLog = new WriteTaskLog(writeFile, fileSystemTools, logger);
	}

	[Fact]
	public async Task WriteTheLogFileToTheLogsFolderForADoneOutcome()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(@"C:\Projects\my-api\tasks\logs\05-refactor-run-process.log", A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteTheLogFileToTheLogsFolderForABlockedOutcome()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Blocked);

		A.CallTo(() => writeFile.Write(@"C:\Projects\my-api\tasks\logs\05-refactor-run-process.log", A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteTheLogFileToTheLogsFolderForAFailedOutcome()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Failed);

		A.CallTo(() => writeFile.Write(@"C:\Projects\my-api\tasks\logs\05-refactor-run-process.log", A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheTaskNameInTheLogBody()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains("05-refactor-run-process.md")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheRepositoryPathInTheLogBody()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains(@"C:\Projects\my-api")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheOutcomeInTheLogBody()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Blocked);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains("Blocked"))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheExitCodeInTheLogBody()
	{
		task.Result.ExitCode = 42;

		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains("42"))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheTimedOutFlagInTheLogBody()
	{
		task.Result.TimedOut = true;

		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains("Timed Out:      true")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheFailedToStartFlagInTheLogBody()
	{
		task.Result.FailedToStart = true;

		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains("Failed To Start:true")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeAllClaudeOutputLinesInTheLogBody()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() =>
				writeFile.Write(
					A<string>._,
					A<string>.That.Matches(content => content.Contains("Line one") && content.Contains("Line two"))
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeAllClaudeErrorLinesInTheLogBody()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains("Error one"))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheReferenceFileNamesInTheLogBody()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains("CLAUDE.md, architecture.md")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ShowNoneWhenThereAreNoReferenceFiles()
	{
		context.ReferenceFiles = new List<ReferenceFile>();

		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => writeFile.Write(A<string>._, A<string>.That.Contains("Reference Files: none")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogThatWeAreWritingTheTaskLog()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() =>
				logger.Information(
					A<string>._,
					A<string>.That.Contains("05-refactor-run-process.md"),
					A<string>.That.Contains("logs")
				)
			)
			.MustHaveHappened();
	}

	[Fact]
	public async Task EnsureTheLogsDirectoryExistsBeforeWriting()
	{
		await writeTaskLog.Write(context, task, TaskOutcome.Done);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\logs")).MustHaveHappenedOnceExactly();
	}
}
