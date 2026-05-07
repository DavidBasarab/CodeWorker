using FatCat.CodeWorker.FileSystem;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IMoveLiveLog
{
	void Move(TaskExecutionContext context, TaskExecution task);
}

public class MoveLiveLog(IMoveFile moveFile, IFileSystemTools fileSystemTools, ILogger logger) : IMoveLiveLog
{
	private static readonly string[] Suffixes = [".live.log", ".transcript.jsonl", ".stderr.log"];

	private static readonly string[] CleanupSuffixes = [".prompt.txt", ".done", ".wrapper.pid"];

	public void Move(TaskExecutionContext context, TaskExecution task)
	{
		var baseFileName = Path.GetFileNameWithoutExtension(task.TaskName);
		var destinationFolder = context.Folders.Logs;

		fileSystemTools.EnsureDirectory(destinationFolder);

		foreach (var suffix in Suffixes)
		{
			MoveOne(baseFileName, suffix, context.Folders.Pending, destinationFolder, task.TaskName);
		}

		foreach (var suffix in CleanupSuffixes)
		{
			DeleteOne(baseFileName, suffix, context.Folders.Pending);
		}
	}

	private void MoveOne(string baseFileName, string suffix, string sourceFolder, string destinationFolder, string taskName)
	{
		var fileName = $"{baseFileName}{suffix}";
		var sourcePath = Path.Combine(sourceFolder, fileName);
		var destinationPath = Path.Combine(destinationFolder, fileName);

		if (!fileSystemTools.FileExists(sourcePath))
		{
			return;
		}

		logger.Information("Moving {FileName} for {TaskName} to {DestinationPath}", fileName, taskName, destinationPath);

		moveFile.Move(sourcePath, destinationPath);
	}

	private void DeleteOne(string baseFileName, string suffix, string sourceFolder)
	{
		var sourcePath = Path.Combine(sourceFolder, $"{baseFileName}{suffix}");

		fileSystemTools.DeleteFile(sourcePath);
	}
}
