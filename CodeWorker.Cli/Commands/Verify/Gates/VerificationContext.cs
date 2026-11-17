using FatCat.CodeWorker.Cli.Commands.Verify.Intent;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Gates;

public class VerificationContext
{
	public string IntentPath { get; set; }

	public string ProductionPath { get; set; }

	public string TestsPath { get; set; }

	public IntentContract Intent { get; set; }
}
