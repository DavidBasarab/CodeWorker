using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IRunTaskCommand : ICommand { }

public class RunCommand(ILogger logger) : IRunTaskCommand
{
	public Task Execute(string[] args)
	{
		logger.Information("Starting task runner");

		return Task.CompletedTask;
	}
}
