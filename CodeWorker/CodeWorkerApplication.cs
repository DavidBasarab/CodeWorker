using Serilog;

namespace FatCat.CodeWorker;

public class CodeWorkerApplication(ILogger logger)
{
	public async Task DoWork(string[] args)
	{
		await Task.CompletedTask;

		logger.Information("Welcome to Code Worker");
	}
}
