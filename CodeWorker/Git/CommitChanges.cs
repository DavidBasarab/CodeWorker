using FatCat.CodeWorker.Process;
using Serilog;

namespace FatCat.CodeWorker.Git;

public interface ICommitChanges
{
	Task<ProcessResult> Commit(string workingDirectory, string message);
}

public class CommitChanges(IRunProcess runProcess, ILogger logger) : ICommitChanges
{
	public async Task<ProcessResult> Commit(string workingDirectory, string message)
	{
		logger.Information("Committing changes in {WorkingDirectory} with message {Message}", workingDirectory, message);

		var addSettings = new ProcessSettings
		{
			FileName = "git",
			Arguments = "add -A",
			WorkingDirectory = workingDirectory,
		};

		var addResult = await runProcess.Run(addSettings);

		if (addResult.ExitCode != 0)
		{
			logger.Warning("Git add failed with exit code {ExitCode}", addResult.ExitCode);

			return addResult;
		}

		var commitSettings = new ProcessSettings
		{
			FileName = "git",
			Arguments = $"commit -m \"{message}\"",
			WorkingDirectory = workingDirectory,
		};

		var commitResult = await runProcess.Run(commitSettings);

		if (commitResult.ExitCode != 0)
		{
			logger.Warning("Git commit failed with exit code {ExitCode}", commitResult.ExitCode);
		}

		return commitResult;
	}
}
