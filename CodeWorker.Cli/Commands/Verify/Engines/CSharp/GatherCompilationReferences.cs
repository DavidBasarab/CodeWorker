using FatCat.Toolkit;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public interface IGatherCompilationReferences
{
	List<string> Gather(string outputDirectory);
}

public class GatherCompilationReferences(
	IFileSystemTools fileSystemTools,
	IReadTrustedPlatformAssemblies readTrustedPlatformAssemblies,
	IInspectAssemblyFile inspectAssemblyFile
) : IGatherCompilationReferences
{
	public List<string> Gather(string outputDirectory)
	{
		var buildOutputAssemblies = FindBuildOutputAssemblies(outputDirectory);
		var frameworkAssemblies = FindFrameworkAssembliesNotIn(buildOutputAssemblies);

		return [.. buildOutputAssemblies, .. frameworkAssemblies];
	}

	private List<string> FindBuildOutputAssemblies(string outputDirectory)
	{
		return fileSystemTools.GetFiles(outputDirectory).Where(IsDll).Where(inspectAssemblyFile.IsManagedAssembly).ToList();
	}

	private List<string> FindFrameworkAssembliesNotIn(List<string> buildOutputAssemblies)
	{
		var runtimeDirectory = readTrustedPlatformAssemblies.RuntimeDirectory;
		var buildOutputFileNames = buildOutputAssemblies.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

		return readTrustedPlatformAssemblies
			.Read()
			.Where(path => path.StartsWith(runtimeDirectory, StringComparison.OrdinalIgnoreCase))
			.Where(path => !buildOutputFileNames.Contains(Path.GetFileName(path)))
			.ToList();
	}

	private static bool IsDll(string path)
	{
		return Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase);
	}
}
