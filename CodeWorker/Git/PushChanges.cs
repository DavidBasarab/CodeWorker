using FatCat.CodeWorker.Process;
using Serilog;

namespace FatCat.CodeWorker.Git;

public interface IPushChanges
{
	Task<ProcessResult> Push(string workingDirectory);
}

public class PushChanges(IRunProcess runProcess, ILogger logger) : IPushChanges
{
	public async Task<ProcessResult> Push(string workingDirectory)
	{
		logger.Information("Pushing changes in {WorkingDirectory}", workingDirectory);

		var settings = new ProcessSettings
		{
			FileName = "git",
			Arguments = "push",
			WorkingDirectory = workingDirectory,
		};

		var result = await runProcess.Run(settings);

		if (result.ExitCode != 0)
		{
			logger.Warning("Git push failed with exit code {ExitCode}", result.ExitCode);
		}

		return result;
	}
}
