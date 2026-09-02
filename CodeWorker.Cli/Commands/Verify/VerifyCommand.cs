using FatCat.CodeWorker.Cli.Commands;
using Serilog;

namespace FatCat.CodeWorker.Cli.Commands.Verify;

public interface IRunVerifyCommand : ICommand { }

public class VerifyCommand(ILogger logger) : IRunVerifyCommand
{
	public Task Execute(string[] args)
	{
		logger.Debug("Verify command invoked");

		return Task.CompletedTask;
	}
}
