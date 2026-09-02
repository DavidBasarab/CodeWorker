using FatCat.CodeWorker.Cli.Commands;
using Serilog;

namespace FatCat.CodeWorker.Cli.Commands.Verify;

public interface IRunVerifyCommand : ICommand { }

public class VerifyCommand(IParseVerifyArguments parseVerifyArguments, ILogger logger) : IRunVerifyCommand
{
	public Task Execute(string[] args)
	{
		var result = parseVerifyArguments.Parse(args);

		if (!result.IsValid)
		{
			logger.Error("verify: {Reason}", result.Message);

			return Task.CompletedTask;
		}

		logger.Information(
			"verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}",
			result.IntentPath,
			result.ProductionPath,
			result.TestsPath
		);

		return Task.CompletedTask;
	}
}
