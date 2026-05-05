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
	public void Move(TaskExecutionContext context, TaskExecution task)
	{
		var baseFileName = Path.GetFileNameWithoutExtension(task.TaskName);
		var liveLogFileName = $"{baseFileName}.live.log";
		var sourcePath = Path.Combine(context.Folders.Pending, liveLogFileName);
		var destinationPath = Path.Combine(context.Folders.Logs, liveLogFileName);

		if (!fileSystemTools.FileExists(sourcePath))
		{
			logger.Information("No live log to move for {TaskName}", task.TaskName);

			return;
		}

		logger.Information("Moving live log for {TaskName} to {DestinationPath}", task.TaskName, destinationPath);

		fileSystemTools.EnsureDirectory(context.Folders.Logs);
		moveFile.Move(sourcePath, destinationPath);
	}
}
