using Serilog;

namespace FatCat.CodeWorker.Cli.Commands;

public interface IProcessArguments
{
	Task Process(string[] args);
}

public class ProcessArguments(ILogger logger) : IProcessArguments
{
	public async Task Process(string[] args)
	{
		if (args.Length == 0)
		{
			logger.Information("No arguments provided");

			return;
		}

		logger.Information("Processing {ArgumentCount} argument(s): {Arguments}", args.Length, string.Join(' ', args));

		await Task.CompletedTask;
	}
}
