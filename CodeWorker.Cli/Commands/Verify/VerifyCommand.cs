using FatCat.CodeWorker.Cli.Commands;
using FatCat.CodeWorker.Cli.Commands.Verify.Intent;
using Serilog;

namespace FatCat.CodeWorker.Cli.Commands.Verify;

public interface IRunVerifyCommand : ICommand { }

public class VerifyCommand(IParseVerifyArguments parseVerifyArguments, IParseIntent parseIntent, ILogger logger)
	: IRunVerifyCommand
{
	public async Task Execute(string[] args)
	{
		var arguments = parseVerifyArguments.Parse(args);

		if (!arguments.IsValid)
		{
			logger.Error("verify: {Reason}", arguments.Message);

			return;
		}

		LogParsedArguments(arguments);

		var intentResult = await parseIntent.Parse(arguments);

		if (!intentResult.IsValid)
		{
			logger.Error("verify: {Reason}", intentResult.Message);

			return;
		}

		LogIntentSummary(intentResult.Intent);
	}

	private void LogParsedArguments(VerifyArgumentsResult arguments)
	{
		logger.Information(
			"verify: parsed intent {IntentPath}, production {ProductionPath}, tests {TestsPath}",
			arguments.IntentPath,
			arguments.ProductionPath,
			arguments.TestsPath
		);
	}

	private void LogIntentSummary(IntentContract intent)
	{
		logger.Information(
			"verify: intent for {Class} declares {BehaviorCount} behaviors",
			intent.Class,
			intent.Behaviors.Count
		);
	}
}
