using FatCat.Toolkit;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public interface IGatherCompilationSources
{
	Task<List<SourceFile>> Gather(
		string productionPath,
		string testsPath,
		ProjectBuild productionBuild,
		ProjectBuild testBuild
	);
}

public class GatherCompilationSources(IFileSystemTools fileSystemTools) : IGatherCompilationSources
{
	public async Task<List<SourceFile>> Gather(
		string productionPath,
		string testsPath,
		ProjectBuild productionBuild,
		ProjectBuild testBuild
	)
	{
		List<string> sourcePaths =
		[
			productionPath,
			testsPath,
			.. DistinctProjects(productionBuild, testBuild).SelectMany(ExistingUsingsFiles),
		];
		List<SourceFile> sources = [];

		foreach (var sourcePath in sourcePaths)
		{
			sources.Add(new SourceFile { FilePath = sourcePath, Text = await fileSystemTools.ReadAllText(sourcePath) });
		}

		return sources;
	}

	private static List<ProjectBuild> DistinctProjects(ProjectBuild productionBuild, ProjectBuild testBuild)
	{
		var isSameProject = string.Equals(
			productionBuild.ProjectFilePath,
			testBuild.ProjectFilePath,
			StringComparison.OrdinalIgnoreCase
		);

		return isSameProject ? [testBuild] : [testBuild, productionBuild];
	}

	private IEnumerable<string> ExistingUsingsFiles(ProjectBuild build)
	{
		return UsingsFiles(build).Where(fileSystemTools.FileExists);
	}

	private static List<string> UsingsFiles(ProjectBuild build)
	{
		var projectName = Path.GetFileNameWithoutExtension(build.ProjectFilePath);

		return
		[
			Path.Combine(build.ProjectDirectory, "GlobalUsings.cs"),
			Path.Combine(build.IntermediateDirectory, $"{projectName}.GlobalUsings.g.cs"),
		];
	}
}
