using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.Process;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface ILogTaskResult
{
	Task Log(string repositoryPath, string taskName, ProcessResult result, List<ReferenceFile> referenceFiles);
}

public class LogTaskResult(IAppendFile appendFile, ILogger logger) : ILogTaskResult
{
	public async Task Log(string repositoryPath, string taskName, ProcessResult result, List<ReferenceFile> referenceFiles)
	{
		var logPath = Path.Combine(repositoryPath, "CodeWorker.log");
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		var referenceFileNames = referenceFiles.Count > 0 ? string.Join(", ", referenceFiles.Select(f => f.Name)) : "none";

		var logEntry = $"""
			[{timestamp}] Task: {taskName} | Exit Code: {result.ExitCode}
			Reference Files: {referenceFileNames}
			Output: {string.Join(Environment.NewLine, result.OutputLines)}
			Errors: {string.Join(Environment.NewLine, result.ErrorLines)}
			---

			""";

		logger.Information("Logging result for task {TaskName} to {LogPath}", taskName, logPath);

		await appendFile.Append(logPath, logEntry);
	}
}
