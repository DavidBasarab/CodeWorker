using FatCat.CodeWorker.Settings;
using Serilog;

namespace FatCat.CodeWorker.Commands.Untrack;

public interface IUntrackRepository
{
	Task Untrack(string repositoryPath);
}

public class UntrackRepository(ILoadAppSettings loadAppSettings, ISaveAppSettings saveAppSettings, ILogger logger)
	: IUntrackRepository
{
	public async Task Untrack(string repositoryPath)
	{
		var settings = await loadAppSettings.Load();

		if (settings.Repositories == null)
		{
			logger.Warning("No repositories are being tracked");

			return;
		}

		var removed = settings.Repositories.RemoveAll(repository =>
			string.Equals(repository.Path, repositoryPath, StringComparison.OrdinalIgnoreCase)
		);

		if (removed == 0)
		{
			logger.Warning("No tracked repository found matching {RepositoryPath}", repositoryPath);

			return;
		}

		logger.Information("Untracked repository {RepositoryPath}", repositoryPath);

		await saveAppSettings.Save(settings);
	}
}
