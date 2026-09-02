namespace FatCat.CodeWorker.Cli.Commands.Verify;

public class VerifyArgumentsResult
{
	public bool IsValid { get; set; }

	public VerifyUsageError Error { get; set; }

	public string Message { get; set; }

	public string IntentPath { get; set; }

	public string ProductionPath { get; set; }

	public string TestsPath { get; set; }
}
