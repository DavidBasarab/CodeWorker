namespace FatCat.CodeWorker.Cli.Commands.Verify;

public enum VerifyUsageError
{
	None,
	NoArguments,
	MissingIntentFlag,
	MissingProductionFlag,
	MissingTestsFlag,
	IntentFileNotFound,
	ProductionFileNotFound,
	TestsFileNotFound,
}
