using FatCat.CodeWorker.Cli.Commands;
using FatCat.CodeWorker.Cli.Commands.Verify.Gates;
using FatCat.CodeWorker.Cli.Commands.Verify.Intent;
using FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;
using FatCat.CodeWorker.Cli.Process;
using Serilog;

namespace FatCat.CodeWorker.Cli.Commands.Verify;

public interface IRunVerifyCommand : ICommand { }

public class VerifyCommand(
	IParseVerifyArguments parseVerifyArguments,
	IParseIntent parseIntent,
	IRunGatePipeline runGatePipeline,
	IBuildVerdict buildVerdict,
	IDeliverVerdict deliverVerdict,
	ISetExitCode setExitCode,
	ILogger logger
) : IRunVerifyCommand
{
	public async Task Execute(string[] args)
	{
		var arguments = parseVerifyArguments.Parse(args);

		if (!arguments.IsValid)
		{
			RejectInvalidArguments(arguments);

			return;
		}

		LogParsedArguments(arguments);

		var intentResult = await parseIntent.Parse(arguments);

		if (!intentResult.IsValid)
		{
			await DeliverInvalidIntent(intentResult, arguments.VerdictPath);

			return;
		}

		LogIntentSummary(intentResult.Intent);

		var context = BuildContext(arguments, intentResult.Intent);

		var pipelineResult = await runGatePipeline.Run(context);

		LogPipelineSummary(pipelineResult);

		await deliverVerdict.Deliver(buildVerdict.Build(pipelineResult), arguments.VerdictPath);
	}

	private void RejectInvalidArguments(VerifyArgumentsResult arguments)
	{
		logger.Error("verify: {Reason}", arguments.Message);

		setExitCode.Set(CliExitCode.InvalidInput);
	}

	private async Task DeliverInvalidIntent(IntentResult intentResult, string verdictPath)
	{
		logger.Error("verify: {Reason}", intentResult.Message);

		await deliverVerdict.Deliver(buildVerdict.BuildInvalidIntent(intentResult), verdictPath);
	}

	private static VerificationContext BuildContext(VerifyArgumentsResult arguments, IntentContract intent)
	{
		return new VerificationContext
		{
			IntentPath = arguments.IntentPath,
			ProductionPath = arguments.ProductionPath,
			TestsPath = arguments.TestsPath,
			Intent = intent,
		};
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

	private void LogPipelineSummary(GatePipelineResult result)
	{
		logger.Information(
			"verify: gate pipeline finished with {GateCount} gates, blocked: {IsBlocked}",
			result.GateResults.Count,
			result.IsBlocked
		);
	}
}
