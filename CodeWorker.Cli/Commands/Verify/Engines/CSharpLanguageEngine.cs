using FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class CSharpLanguageEngine(
	IFindProjectBuild findProjectBuild,
	IGatherCompilationSources gatherCompilationSources,
	IGatherCompilationReferences gatherCompilationReferences,
	ICompileInMemory compileInMemory
) : ILanguageEngine
{
	public string Language
	{
		get { return "C#"; }
	}

	public IReadOnlyList<string> FileExtensions
	{
		get { return [".cs"]; }
	}

	public async Task<CompilationResult> Compile(string productionPath, string testsPath)
	{
		var testBuildResult = await findProjectBuild.Find(testsPath);

		if (testBuildResult.Error != EngineError.None)
		{
			return EngineFailure(testBuildResult);
		}

		var productionBuildResult = await findProjectBuild.Find(productionPath);

		if (productionBuildResult.Error != EngineError.None)
		{
			return EngineFailure(productionBuildResult);
		}

		return await CompileOverlay(productionPath, testsPath, productionBuildResult.Build, testBuildResult.Build);
	}

	private static CompilationResult EngineFailure(ProjectBuildResult projectBuildResult)
	{
		return new CompilationResult { Error = projectBuildResult.Error, Message = projectBuildResult.Message };
	}

	private async Task<CompilationResult> CompileOverlay(
		string productionPath,
		string testsPath,
		ProjectBuild productionBuild,
		ProjectBuild testBuild
	)
	{
		var sources = await gatherCompilationSources.Gather(productionPath, testsPath, productionBuild, testBuild);
		var references = gatherCompilationReferences.Gather(testBuild.OutputDirectory);

		var result = compileInMemory.Compile($"{testBuild.AssemblyName}.Verify", sources, references);

		result.BuildOutputDirectory = testBuild.OutputDirectory;

		return result;
	}
}
