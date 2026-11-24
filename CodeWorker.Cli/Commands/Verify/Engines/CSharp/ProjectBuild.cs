namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public class ProjectBuild
{
	public string ProjectFilePath { get; set; }

	public string ProjectDirectory { get; set; }

	public string AssemblyName { get; set; }

	public string TargetFramework { get; set; }

	public string OutputDirectory { get; set; }

	public string IntermediateDirectory { get; set; }
}
