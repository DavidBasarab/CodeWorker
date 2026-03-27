using System.Reflection;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Setup;

public interface ISetupRepository
{
	Task Setup(string repositoryPath);
}

public class SetupRepository(IFileSystemTools fileSystemTools, ILogger logger) : ISetupRepository
{
	private static string ReadReadmeFromEmbeddedResource()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var resourceName = "FatCat.CodeWorker.Commands.Setup.TasksReadme.md";

		using var stream = assembly.GetManifestResourceStream(resourceName);
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}

	public async Task Setup(string repositoryPath)
	{
		var tasksPath = Path.Combine(repositoryPath, "tasks");
		var todoPath = Path.Combine(tasksPath, "todo");
		var donePath = Path.Combine(tasksPath, "done");
		var blockedPath = Path.Combine(tasksPath, "blocked");

		logger.Information("Setting up task folders at {RepositoryPath}", repositoryPath);

		fileSystemTools.EnsureDirectory(todoPath);
		fileSystemTools.EnsureDirectory(donePath);
		fileSystemTools.EnsureDirectory(blockedPath);

		await fileSystemTools.WriteAllText(Path.Combine(todoPath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(donePath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(blockedPath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(tasksPath, "README.md"), ReadReadmeFromEmbeddedResource());

		logger.Information("Repository setup complete at {RepositoryPath}", repositoryPath);
	}
}
