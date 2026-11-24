namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public enum EngineError
{
	None,
	ProjectNotFound,
	MultipleProjectsFound,
	ProjectFileUnreadable,
	TargetFrameworkNotFound,
	BuildOutputNotFound,
}
