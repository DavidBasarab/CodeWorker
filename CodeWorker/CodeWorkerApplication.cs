using FatCat.CodeWorker.Commands;
using Serilog;

namespace FatCat.CodeWorker;

public class CodeWorkerApplication(IResolveCommand resolveCommand, ILogger logger)
{
	public async Task DoWork(string[] args)
	{
		logger.Information("Welcome to Code Worker");

		var command = resolveCommand.Resolve(args);

		await command.Execute(args);
	}
}
