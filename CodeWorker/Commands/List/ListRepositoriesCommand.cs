using FatCat.CodeWorker.Settings;
using Serilog;

namespace FatCat.CodeWorker.Commands.List;

public interface IRunListCommand : ICommand { }

public class ListRepositoriesCommand(ILoadAppSettings loadAppSettings, ILogger logger) : IRunListCommand
{
	public async Task Execute(string[] args)
	{
		var settings = await loadAppSettings.Load();

		if (settings.Repositories == null || settings.Repositories.Count == 0)
		{
			logger.Information("No repositories are being tracked");

			return;
		}

		logger.Information("Tracked repositories ({Count}):", settings.Repositories.Count);

		foreach (var repository in settings.Repositories)
		{
			logger.Information("  {RepositoryPath} (Enabled: {Enabled})", repository.Path, repository.Enabled);
		}
	}
}
