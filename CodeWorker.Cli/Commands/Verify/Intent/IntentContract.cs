namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public class IntentContract
{
	public string Why { get; set; }

	public string Class { get; set; }

	public string Responsibility { get; set; }

	public List<IntentBehavior> Behaviors { get; set; } = [];
}
