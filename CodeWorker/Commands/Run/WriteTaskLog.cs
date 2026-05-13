using FatCat.CodeWorker.FileSystem;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IWriteTaskLog
{
	Task Write(TaskExecutionContext context, TaskExecution task, TaskOutcome outcome);
}

public class WriteTaskLog(IWriteFile writeFile, IFileSystemTools fileSystemTools, ILogger logger) : IWriteTaskLog
{
	public async Task Write(TaskExecutionContext context, TaskExecution task, TaskOutcome outcome)
	{
		var destinationFolder = context.Folders.Logs;

		fileSystemTools.EnsureDirectory(destinationFolder);

		var logFileName = $"{Path.GetFileNameWithoutExtension(task.TaskName)}.log";
		var logFilePath = Path.Combine(destinationFolder, logFileName);

		logger.Information("Writing task log for {TaskName} to {LogFilePath}", task.TaskName, logFilePath);

		var referenceFileNames =
			context.ReferenceFiles.Count > 0 ? string.Join(", ", context.ReferenceFiles.Select(f => f.Name)) : "none";

		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		var output = string.Join(Environment.NewLine, task.Result.OutputLines);
		var errors = string.Join(Environment.NewLine, task.Result.ErrorLines);

		var content = $"""
			Task:           {task.TaskName}
			Repository:     {context.Repository.Path}
			Outcome:        {outcome}
			Timestamp:      {timestamp}
			Exit Code:      {task.Result.ExitCode}
			Timed Out:      {task.Result.TimedOut.ToString().ToLowerInvariant()}
			Failed To Start:{task.Result.FailedToStart.ToString().ToLowerInvariant()}
			Reference Files: {referenceFileNames}

			----- Claude Output -----
			{output}

			----- Claude Errors -----
			{errors}
			""";

		await writeFile.Write(logFilePath, content);
	}
}
