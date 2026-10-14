namespace FatCat.CodeWorker.Cli.Commands.Verify.Intent;

public enum IntentValidationError
{
	None,
	IntentFileUnreadable,
	MalformedIntentJson,
	MissingWhy,
	MissingClass,
	ClassDoesNotMatchProductionFile,
	MissingResponsibility,
	NoBehaviors,
	EmptyBehavior,
	NonObservableBehavior,
	InvalidTestName,
}
