namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public class ProjectBuildResult
{
	public EngineError Error { get; set; }

	public string Message { get; set; }

	public ProjectBuild Build { get; set; }
}
