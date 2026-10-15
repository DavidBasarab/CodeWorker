namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public class IntentResult
{
	public bool IsValid { get; set; }

	public IntentValidationError Error { get; set; }

	public string Message { get; set; }

	public IntentContract Intent { get; set; }
}
