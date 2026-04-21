using System.Reflection;
using FatCat.CodeWorker.Commands.Track;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Setup;

public interface ISetupRepository
{
	Task Setup(string repositoryPath);
}

public class SetupRepository(IFileSystemTools fileSystemTools, ITrackRepository trackRepository, ILogger logger)
	: ISetupRepository
{
	private static string ReadEmbeddedResource(string resourceName)
	{
		var assembly = Assembly.GetExecutingAssembly();
		var fullResourceName = $"FatCat.CodeWorker.Commands.Setup.{resourceName}";

		using var stream = assembly.GetManifestResourceStream(fullResourceName);
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}

	public async Task Setup(string repositoryPath)
	{
		var tasksPath = Path.Combine(repositoryPath, "tasks");
		var todoPath = Path.Combine(tasksPath, "todo");
		var donePath = Path.Combine(tasksPath, "done");
		var blockedPath = Path.Combine(tasksPath, "blocked");
		var failedPath = Path.Combine(tasksPath, "failed");
		var pendingPath = Path.Combine(tasksPath, "pending");

		logger.Information("Setting up task folders at {RepositoryPath}", repositoryPath);

		fileSystemTools.EnsureDirectory(todoPath);
		fileSystemTools.EnsureDirectory(donePath);
		fileSystemTools.EnsureDirectory(blockedPath);
		fileSystemTools.EnsureDirectory(failedPath);
		fileSystemTools.EnsureDirectory(pendingPath);

		await fileSystemTools.WriteAllText(Path.Combine(todoPath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(donePath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(blockedPath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(failedPath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(pendingPath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(tasksPath, "README.md"), ReadEmbeddedResource("TasksReadme.md"));
		await fileSystemTools.WriteAllText(
			Path.Combine(tasksPath, "settings.json"),
			ReadEmbeddedResource("defaultSettings.json")
		);

		logger.Information("Repository setup complete at {RepositoryPath}", repositoryPath);

		await trackRepository.Track(repositoryPath);
	}
}
