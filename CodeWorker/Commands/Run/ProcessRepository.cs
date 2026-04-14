using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Git;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IProcessRepository
{
	Task Process(RepositorySettings repository, ClaudeSettings globalClaudeSettings);
}

public class ProcessRepository(
	IValidateRepository validateRepository,
	ILoadRepoSettings loadRepoSettings,
	IDiscoverTasks discoverTasks,
	IMoveTask moveTask,
	IRunClaude runClaude,
	ILogTaskResult logTaskResult,
	IGenerateBlockedExplanation generateBlockedExplanation,
	ICommitChanges commitChanges,
	IPushChanges pushChanges,
	ILogger logger
) : IProcessRepository
{
	public async Task Process(RepositorySettings repository, ClaudeSettings globalClaudeSettings)
	{
		var validationResult = validateRepository.Validate(repository);

		if (!validationResult.IsValid)
		{
			logger.Error(
				"Repository {RepositoryPath} is broken — skipping. Errors: {Errors}",
				repository.Path,
				string.Join("; ", validationResult.Errors)
			);

			return;
		}

		var repoSettings = await loadRepoSettings.Load(repository.Path);

		if (!repoSettings.Enabled)
		{
			logger.Information("Repository {RepositoryPath} is disabled, skipping", repository.Path);

			return;
		}

		var effectiveClaudeSettings = globalClaudeSettings.MergeWith(repoSettings.Claude);

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

			var result = await runClaude.Run(pendingFilePath, effectiveClaudeSettings);

			if (repoSettings.LogResults)
			{
				await logTaskResult.Log(repository.Path, taskName, result);
			}

			var isBlocked = result.ExitCode != 0;

			if (isBlocked)
			{
				moveTask.Move(pendingFilePath, blockedFolder);
				await generateBlockedExplanation.Generate(blockedFolder, taskName, result);

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

				if (repoSettings.Git?.CommitAfterEachTask == true)
				{
					var taskNameWithoutExtension = Path.GetFileNameWithoutExtension(taskFile);
					var commitMessage = $"{repoSettings.Git.CommitMessagePrefix} {taskNameWithoutExtension}";

					var commitResult = await commitChanges.Commit(repository.Path, commitMessage);

					if (repoSettings.LogResults)
					{
						await logTaskResult.Log(repository.Path, taskName, commitResult);
					}

					if (commitResult.ExitCode != 0)
					{
						logger.Error("Commit failed for task {TaskName}, stopping repository processing", taskName);

						return;
					}
				}

				if (repoSettings.Git?.PushAfterEachTask == true)
				{
					var pushResult = await pushChanges.Push(repository.Path);

					if (repoSettings.LogResults)
					{
						await logTaskResult.Log(repository.Path, taskName, pushResult);
					}

					if (pushResult.ExitCode != 0)
					{
						logger.Error(
							"Push failed for task {TaskName}, possible merge conflict — stopping repository processing",
							taskName
						);

						return;
					}
				}
			}
		}
	}
}
