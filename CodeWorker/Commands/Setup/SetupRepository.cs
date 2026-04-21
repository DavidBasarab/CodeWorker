using FatCat.CodeWorker.Commands.Track;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Setup;

public interface ISetupRepository
{
	Task Setup(string repositoryPath);
}

public class SetupRepository(
	IFileSystemTools fileSystemTools,
	IReadEmbeddedResource readEmbeddedResource,
	ITrackRepository trackRepository,
	ILogger logger
) : ISetupRepository
{
	public async Task Setup(string repositoryPath)
	{
		var tasksPath = Path.Combine(repositoryPath, "tasks");

		logger.Information("Setting up task folders at {RepositoryPath}", repositoryPath);

		foreach (var folder in RequiredTaskFolders.All)
		{
			var folderPath = Path.Combine(tasksPath, folder);

			fileSystemTools.EnsureDirectory(folderPath);
			await fileSystemTools.WriteAllText(Path.Combine(folderPath, ".gitkeep"), string.Empty);
		}

		await fileSystemTools.WriteAllText(Path.Combine(tasksPath, "README.md"), readEmbeddedResource.Read("TasksReadme.md"));

		await fileSystemTools.WriteAllText(
			Path.Combine(tasksPath, "settings.json"),
			readEmbeddedResource.Read("defaultSettings.json")
		);

		logger.Information("Repository setup complete at {RepositoryPath}", repositoryPath);

		await trackRepository.Track(repositoryPath);
	}
}
