using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Track;

public interface ITrackRepository
{
	Task Track(string repositoryPath);
}

public class TrackRepository(
	ILoadAppSettings loadAppSettings,
	ISaveAppSettings saveAppSettings,
	IFileSystemTools fileSystemTools,
	ILogger logger
) : ITrackRepository
{
	public async Task Track(string repositoryPath)
	{
		var tasksPath = Path.Combine(repositoryPath, "tasks");

		if (!fileSystemTools.DirectoryExists(tasksPath))
		{
			logger.Warning("Cannot track {RepositoryPath}: tasks folder not found. Run setup first.", repositoryPath);

			return;
		}

		var settings = await loadAppSettings.Load();

		settings.Repositories ??= new List<RepositorySettings>();

		if (IsAlreadyTracked(settings, repositoryPath))
		{
			logger.Information("Repository {RepositoryPath} is already tracked", repositoryPath);

			return;
		}

		var newEntry = new RepositorySettings
		{
			Path = repositoryPath,
			Enabled = true,
			SettingsPath = Path.Combine(tasksPath, "settings.json"),
		};

		settings.Repositories.Add(newEntry);

		logger.Information("Tracking repository {RepositoryPath}", repositoryPath);

		await saveAppSettings.Save(settings);
	}

	private static bool IsAlreadyTracked(CodeWorkerSettings settings, string repositoryPath)
	{
		return settings.Repositories.Any(repository =>
			string.Equals(repository.Path, repositoryPath, StringComparison.OrdinalIgnoreCase)
		);
	}
}
