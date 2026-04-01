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

		var settings = await loadAppSettings.Load();

		foreach (var repository in settings.Repositories)
		{
			await processRepository.Process(repository);
		}

		logger.Information("Task runner complete");
	}
}
