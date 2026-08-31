using FatCat.CodeWorker.Cli.Commands;
using Serilog;

namespace FatCat.CodeWorker.Cli;

public class CodeWorkerCliApplication(IProcessArguments processArguments, ILogger logger)
{
	public async Task Run(string[] args)
	{
		logger.Information("Welcome to Code Worker CLI");

		await processArguments.Process(args);
	}
}
