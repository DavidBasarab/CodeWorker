using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IProcessRepository
{
	Task Process(RepositorySettings repository);
}

public class ProcessRepository(
	ILoadRepoSettings loadRepoSettings,
	IDiscoverTasks discoverTasks,
	IMoveTask moveTask,
	IRunClaude runClaude,
	ILogTaskResult logTaskResult,
	ILogger logger
) : IProcessRepository
{
	public async Task Process(RepositorySettings repository)
	{
		var repoSettings = await loadRepoSettings.Load(repository.Path);

		if (!repoSettings.Enabled)
		{
			logger.Information("Repository {RepositoryPath} is disabled, skipping", repository.Path);

			return;
		}

		var todoFolder = Path.Combine(repository.Path, repoSettings.Tasks.TodoFolder);
		var pendingFolder = Path.Combine(repository.Path, repoSettings.Tasks.PendingFolder);
		var doneFolder = Path.Combine(repository.Path, repoSettings.Tasks.DoneFolder);
		var blockedFolder = Path.Combine(repository.Path, repoSettings.Tasks.BlockedFolder);

		var taskFiles = discoverTasks.Discover(todoFolder);

		foreach (var taskFile in taskFiles)
		{
			var taskName = Path.GetFileName(taskFile);
			var pendingFilePath = Path.Combine(pendingFolder, taskName);

			logger.Information("Starting task {TaskName}", taskName);

			moveTask.Move(taskFile, pendingFolder);

			var result = await runClaude.Run(pendingFilePath);

			if (repoSettings.LogResults)
			{
				await logTaskResult.Log(repository.Path, taskName, result);
			}

			var isBlocked = result.ExitCode != 0;

			if (isBlocked)
			{
				moveTask.Move(pendingFilePath, blockedFolder);

				if (repoSettings.Tasks.StopOnBlocked)
				{
					logger.Warning(
						"Task {TaskName} was blocked and StopOnBlocked is enabled, stopping repository processing",
						taskName
					);

					return;
				}
			}
			else
			{
				moveTask.Move(pendingFilePath, doneFolder);
			}
		}
	}
}
