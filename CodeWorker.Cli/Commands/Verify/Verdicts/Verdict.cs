namespace FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public class Verdict
{
	public VerdictStatus Status { get; set; }

	public string Message { get; set; }

	public List<GateVerdict> Gates { get; set; } = [];
}
