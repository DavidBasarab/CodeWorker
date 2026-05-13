using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class CleanPendingArtifactsTests
{
	private readonly IMoveFile moveFile;
	private readonly IFileSystemTools fileSystemTools;
	private readonly ITranscriptPaths transcriptPaths;
	private readonly ILogger logger;
	private readonly CleanPendingArtifacts cleanPendingArtifacts;
	private readonly TaskExecutionContext context;
	private readonly TaskExecution task;
	private readonly TranscriptPaths paths;
	private bool fileExists;

	public CleanPendingArtifactsTests()
	{
		moveFile = A.Fake<IMoveFile>();
		fileSystemTools = A.Fake<IFileSystemTools>();
		transcriptPaths = A.Fake<ITranscriptPaths>();
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

		paths = new TranscriptPaths
		{
			TaskName = "05-refactor-run-process.md",
			PromptFile = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.prompt.txt",
			TranscriptFile = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.transcript.jsonl",
			StderrFile = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.stderr.log",
			DoneSentinel = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.done",
			PidFile = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.wrapper.pid",
			LiveLogFile = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.live.log",
			WrapperStartedFile = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.wrapper.started",
			WrapperLogFile = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.wrapper.log",
			ClaudeArgsFile = @"C:\Projects\my-api\tasks\pending\05-refactor-run-process.claude-args.txt",
		};

		A.CallTo(() => transcriptPaths.For(task.PendingFilePath)).Returns(paths);

		cleanPendingArtifacts = new CleanPendingArtifacts(moveFile, fileSystemTools, transcriptPaths, logger);
	}

	[Fact]
	public void DeleteEverySiblingFromPendingWhenOutcomeIsDone()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Done);

		A.CallTo(() => fileSystemTools.DeleteFile(paths.PromptFile)).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile(paths.TranscriptFile)).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile(paths.StderrFile)).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile(paths.DoneSentinel)).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile(paths.PidFile)).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile(paths.LiveLogFile)).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile(paths.WrapperStartedFile)).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile(paths.WrapperLogFile)).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile(paths.ClaudeArgsFile)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DoNotMoveAnyFileWhenOutcomeIsDone()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Done);

		A.CallTo(() => moveFile.Move(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public void MoveEverySiblingToFailedFolderWhenOutcomeIsFailed()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Failed);

		A.CallTo(() => moveFile.Move(paths.PromptFile, @"C:\Projects\my-api\tasks\failed\05-refactor-run-process.prompt.txt"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() =>
				moveFile.Move(paths.TranscriptFile, @"C:\Projects\my-api\tasks\failed\05-refactor-run-process.transcript.jsonl")
			)
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => moveFile.Move(paths.StderrFile, @"C:\Projects\my-api\tasks\failed\05-refactor-run-process.stderr.log"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => moveFile.Move(paths.DoneSentinel, @"C:\Projects\my-api\tasks\failed\05-refactor-run-process.done"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => moveFile.Move(paths.PidFile, @"C:\Projects\my-api\tasks\failed\05-refactor-run-process.wrapper.pid"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => moveFile.Move(paths.LiveLogFile, @"C:\Projects\my-api\tasks\failed\05-refactor-run-process.live.log"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() =>
				moveFile.Move(
					paths.WrapperStartedFile,
					@"C:\Projects\my-api\tasks\failed\05-refactor-run-process.wrapper.started"
				)
			)
			.MustHaveHappenedOnceExactly();
		A.CallTo(() =>
				moveFile.Move(paths.WrapperLogFile, @"C:\Projects\my-api\tasks\failed\05-refactor-run-process.wrapper.log")
			)
			.MustHaveHappenedOnceExactly();
		A.CallTo(() =>
				moveFile.Move(paths.ClaudeArgsFile, @"C:\Projects\my-api\tasks\failed\05-refactor-run-process.claude-args.txt")
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void MoveEverySiblingToBlockedFolderWhenOutcomeIsBlocked()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Blocked);

		A.CallTo(() => moveFile.Move(paths.PromptFile, @"C:\Projects\my-api\tasks\blocked\05-refactor-run-process.prompt.txt"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => moveFile.Move(paths.LiveLogFile, @"C:\Projects\my-api\tasks\blocked\05-refactor-run-process.live.log"))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() =>
				moveFile.Move(paths.ClaudeArgsFile, @"C:\Projects\my-api\tasks\blocked\05-refactor-run-process.claude-args.txt")
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DoNotDeleteAnyFileWhenOutcomeIsFailed()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Failed);

		A.CallTo(() => fileSystemTools.DeleteFile(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public void DoNotDeleteAnyFileWhenOutcomeIsBlocked()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Blocked);

		A.CallTo(() => fileSystemTools.DeleteFile(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public void SkipMissingSiblingsOnDone()
	{
		fileExists = false;

		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Done);

		A.CallTo(() => fileSystemTools.DeleteFile(A<string>._)).MustNotHaveHappened();
		A.CallTo(() => moveFile.Move(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public void SkipMissingSiblingsOnFailed()
	{
		fileExists = false;

		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Failed);

		A.CallTo(() => moveFile.Move(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public void SkipMissingSiblingsOnBlocked()
	{
		fileExists = false;

		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Blocked);

		A.CallTo(() => moveFile.Move(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public void EnsureFailedFolderExistsBeforeMoving()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Failed);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\failed"))
			.MustHaveHappenedOnceExactly()
			.Then(A.CallTo(() => moveFile.Move(A<string>._, A<string>._)).MustHaveHappened());
	}

	[Fact]
	public void EnsureBlockedFolderExistsBeforeMoving()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Blocked);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\blocked"))
			.MustHaveHappenedOnceExactly()
			.Then(A.CallTo(() => moveFile.Move(A<string>._, A<string>._)).MustHaveHappened());
	}

	[Fact]
	public void LogOncePerFileDeletedOnDone()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Done);

		A.CallTo(logger).Where(call => call.Method.Name == "Information").MustHaveHappened(9, Times.Exactly);
	}

	[Fact]
	public void LogOncePerFileMovedOnFailed()
	{
		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Failed);

		A.CallTo(logger).Where(call => call.Method.Name == "Information").MustHaveHappened(9, Times.Exactly);
	}

	[Fact]
	public void ThrowArgumentOutOfRangeForUnknownOutcome()
	{
		var act = () => cleanPendingArtifacts.Clean(context, task, (TaskOutcome)999);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void IterateEverySiblingFromTranscriptPaths()
	{
		var sentinelPaths = new TranscriptPaths
		{
			TaskName = "sentinel.md",
			PromptFile = "sentinel-prompt",
			TranscriptFile = "sentinel-transcript",
			StderrFile = "sentinel-stderr",
			DoneSentinel = "sentinel-done",
			PidFile = "sentinel-pid",
			LiveLogFile = "sentinel-live",
			WrapperStartedFile = "sentinel-started",
			WrapperLogFile = "sentinel-wrapper",
			ClaudeArgsFile = "sentinel-args",
		};

		A.CallTo(() => transcriptPaths.For(task.PendingFilePath)).Returns(sentinelPaths);

		cleanPendingArtifacts.Clean(context, task, TaskOutcome.Done);

		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-prompt")).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-transcript")).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-stderr")).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-done")).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-pid")).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-live")).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-started")).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-wrapper")).MustHaveHappenedOnceExactly();
		A.CallTo(() => fileSystemTools.DeleteFile("sentinel-args")).MustHaveHappenedOnceExactly();
	}
}
