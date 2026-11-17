namespace FatCat.CodeWorker.Cli.Commands.Verify.Gates;

public class GatePipelineResult
{
	public bool IsBlocked { get; set; }

	public List<GateResult> GateResults { get; set; } = [];
}
