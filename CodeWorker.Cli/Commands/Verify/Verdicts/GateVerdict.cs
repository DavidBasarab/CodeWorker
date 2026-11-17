using FatCat.CodeWorker.Cli.Commands.Verify.Gates;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public class GateVerdict
{
	public string Name { get; set; }

	public GateStage Stage { get; set; }

	public GateStatus Status { get; set; }

	public long DurationMilliseconds { get; set; }

	public List<GateFinding> Findings { get; set; } = [];
}
