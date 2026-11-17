namespace FatCat.CodeWorker.Cli.Commands.Verify.Gates;

public class GateResult
{
	public string GateName { get; set; }

	public GateStage Stage { get; set; }

	public GateStatus Status { get; set; }

	public List<GateFinding> Findings { get; set; } = [];

	public TimeSpan Duration { get; set; }
}
