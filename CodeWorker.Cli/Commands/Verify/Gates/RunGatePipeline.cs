using FatCat.CodeWorker.Cli.Time;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Gates;

public interface IRunGatePipeline
{
	Task<GatePipelineResult> Run(VerificationContext context);
}

public class RunGatePipeline(IEnumerable<IVerificationGate> gates, IClock clock) : IRunGatePipeline
{
	public async Task<GatePipelineResult> Run(VerificationContext context)
	{
		List<GateResult> gateResults = [];

		foreach (var gate in GatesInStageOrder())
		{
			var gateResult = HasAnyFailed(gateResults) ? SkippedResult(gate) : await RunGate(gate, context);

			gateResults.Add(gateResult);
		}

		return new GatePipelineResult { IsBlocked = HasAnyFailed(gateResults), GateResults = gateResults };
	}

	private IEnumerable<IVerificationGate> GatesInStageOrder()
	{
		return gates.OrderBy(gate => gate.Stage);
	}

	private static bool HasAnyFailed(List<GateResult> gateResults)
	{
		return gateResults.Any(gateResult => gateResult.Status == GateStatus.Failed);
	}

	private async Task<GateResult> RunGate(IVerificationGate gate, VerificationContext context)
	{
		var startedAt = clock.UtcNow;
		var gateResult = await gate.Run(context);
		var finishedAt = clock.UtcNow;

		gateResult.GateName = gate.Name;
		gateResult.Stage = gate.Stage;
		gateResult.Duration = finishedAt - startedAt;

		return gateResult;
	}

	private static GateResult SkippedResult(IVerificationGate gate)
	{
		return new GateResult
		{
			GateName = gate.Name,
			Stage = gate.Stage,
			Status = GateStatus.Skipped,
		};
	}
}
