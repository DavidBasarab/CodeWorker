using FatCat.CodeWorker.Process;
using Serilog;

namespace FatCat.CodeWorker.Git;

public interface IPullChanges
{
	Task<ProcessResult> Pull(string workingDirectory);
}

public class PullChanges(IRunProcess runProcess, ILogger logger) : IPullChanges
{
	public async Task<ProcessResult> Pull(string workingDirectory)
	{
		logger.Information("Pulling latest changes in {WorkingDirectory}", workingDirectory);

		var settings = new ProcessSettings
		{
			FileName = "git",
			Arguments = "pull",
			WorkingDirectory = workingDirectory,
		};

		var result = await runProcess.Run(settings);

		if (result.ExitCode != 0)
		{
			logger.Warning("Git pull failed with exit code {ExitCode}", result.ExitCode);
		}

		return result;
	}
}
