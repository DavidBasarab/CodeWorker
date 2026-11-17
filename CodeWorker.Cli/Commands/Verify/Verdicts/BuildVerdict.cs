using FatCat.CodeWorker.Cli.Commands.Verify.Gates;
using FatCat.CodeWorker.Cli.Commands.Verify.Intent;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public interface IBuildVerdict
{
	Verdict Build(GatePipelineResult pipelineResult);

	Verdict BuildInvalidIntent(IntentResult intentResult);
}

public class BuildVerdict : IBuildVerdict
{
	public Verdict Build(GatePipelineResult pipelineResult)
	{
		return new Verdict
		{
			Status = pipelineResult.IsBlocked ? VerdictStatus.Blocked : VerdictStatus.Green,
			Message = pipelineResult.IsBlocked ? BlockedMessage(pipelineResult) : "No gate failed.",
			Gates = [.. pipelineResult.GateResults.Select(ToGateVerdict)],
		};
	}

	public Verdict BuildInvalidIntent(IntentResult intentResult)
	{
		return new Verdict
		{
			Status = VerdictStatus.InvalidIntent,
			Message = intentResult.Message,
			Gates = [],
		};
	}

	private static string BlockedMessage(GatePipelineResult pipelineResult)
	{
		var failedGate = pipelineResult.GateResults.First(gateResult => gateResult.Status == GateStatus.Failed);
		var firstFinding = failedGate.Findings.FirstOrDefault();

		if (firstFinding == null)
		{
			return $"{failedGate.GateName} failed.";
		}

		return $"{failedGate.GateName} failed: {firstFinding.Reason}{Location(firstFinding)}";
	}

	private static string Location(GateFinding finding)
	{
		if (finding.FilePath == null)
		{
			return string.Empty;
		}

		if (finding.Line == null)
		{
			return $" ({finding.FilePath})";
		}

		return $" ({finding.FilePath}:{finding.Line})";
	}

	private static GateVerdict ToGateVerdict(GateResult gateResult)
	{
		return new GateVerdict
		{
			Name = gateResult.GateName,
			Stage = gateResult.Stage,
			Status = gateResult.Status,
			DurationMilliseconds = (long)gateResult.Duration.TotalMilliseconds,
			Findings = [.. gateResult.Findings],
		};
	}
}
