using System.Diagnostics;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IRunTaskCommand : ICommand { }

public class RunCommand(ILoadAppSettings loadAppSettings, IProcessRepository processRepository, ILogger logger)
	: IRunTaskCommand
{
	public async Task Execute(string[] args)
	{
		logger.Information("Starting task runner");

		var stopwatch = Stopwatch.StartNew();

		var settings = await loadAppSettings.Load();

		if (settings.Repositories.Count == 0)
		{
			logger.Warning("No repositories configured in app settings");
		}
		else
		{
			logger.Information("Found {RepositoryCount} repository(ies) to process", settings.Repositories.Count);
		}

		foreach (var repository in settings.Repositories)
		{
			logger.Information("Processing repository {RepositoryPath}", repository.Path);
			await processRepository.Process(repository, settings.Claude);
		}

		stopwatch.Stop();

		logger.Information(
			"Task runner complete — processed {RepositoryCount} repositories in {DurationSeconds}s",
			settings.Repositories.Count,
			stopwatch.Elapsed.TotalSeconds
		);
	}
}
