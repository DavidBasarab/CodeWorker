using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.FileSystem;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface ICleanPendingArtifacts
{
	void Clean(TaskExecutionContext context, TaskExecution task, TaskOutcome outcome);
}

public class CleanPendingArtifacts(
	IMoveFile moveFile,
	IFileSystemTools fileSystemTools,
	ITranscriptPaths transcriptPaths,
	ILogger logger
) : ICleanPendingArtifacts
{
	public void Clean(TaskExecutionContext context, TaskExecution task, TaskOutcome outcome)
	{
		var paths = transcriptPaths.For(task.PendingFilePath);
		var siblings = EnumerateSiblings(paths);
		var destination = ResolveDestination(outcome, context.Folders);

		if (destination == null)
		{
			DeleteAll(siblings, task.TaskName);

			return;
		}

		fileSystemTools.EnsureDirectory(destination);

		MoveAll(siblings, destination, task.TaskName);
	}

	private static IReadOnlyList<string> EnumerateSiblings(TranscriptPaths paths)
	{
		return
		[
			paths.PromptFile,
			paths.TranscriptFile,
			paths.StderrFile,
			paths.DoneSentinel,
			paths.PidFile,
			paths.LiveLogFile,
			paths.WrapperStartedFile,
			paths.WrapperLogFile,
			paths.ClaudeArgsFile,
		];
	}

	private static string ResolveDestination(TaskOutcome outcome, TaskFolders folders)
	{
		return outcome switch
		{
			TaskOutcome.Done => null,
			TaskOutcome.Blocked => folders.Blocked,
			TaskOutcome.Failed => folders.Failed,
			_ => throw new ArgumentOutOfRangeException(nameof(outcome)),
		};
	}

	private void DeleteAll(IReadOnlyList<string> sources, string taskName)
	{
		foreach (var source in sources)
		{
			if (!fileSystemTools.FileExists(source))
			{
				continue;
			}

			logger.Information("Deleting {SourcePath} for {TaskName}", source, taskName);

			fileSystemTools.DeleteFile(source);
		}
	}

	private void MoveAll(IReadOnlyList<string> sources, string destinationFolder, string taskName)
	{
		foreach (var source in sources)
		{
			if (!fileSystemTools.FileExists(source))
			{
				continue;
			}

			var destinationPath = Path.Combine(destinationFolder, Path.GetFileName(source));

			logger.Information("Moving {SourcePath} for {TaskName} to {DestinationPath}", source, taskName, destinationPath);

			moveFile.Move(source, destinationPath);
		}
	}
}
