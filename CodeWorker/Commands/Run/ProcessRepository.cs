using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Git;
using FatCat.CodeWorker.History;
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
	IGenerateFailedExplanation generateFailedExplanation,
	IClassifyTaskResult classifyTaskResult,
	ICollectReferenceFiles collectReferenceFiles,
	ICommitChanges commitChanges,
	IPushChanges pushChanges,
	IRecordRunHistory recordRunHistory,
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
		var failedFolder = Path.Combine(repository.Path, repoSettings.Tasks.FailedFolder);
		var referenceFolder = Path.Combine(repository.Path, repoSettings.Tasks.ReferenceFolder);

		var referenceFiles = await collectReferenceFiles.Collect(referenceFolder);

		if (referenceFiles.Count > 0)
		{
			var fileNames = string.Join(", ", referenceFiles.Select(f => f.Name));

			logger.Information("Including {Count} reference file(s): {FileNames}", referenceFiles.Count, fileNames);
		}

		var taskFiles = discoverTasks.Discover(todoFolder);

		foreach (var taskFile in taskFiles)
		{
			var taskName = Path.GetFileName(taskFile);
			var pendingFilePath = Path.Combine(pendingFolder, taskName);

			logger.Information("Starting task {TaskName}", taskName);

			moveTask.Move(taskFile, pendingFolder);

			var result = await runClaude.Run(pendingFilePath, effectiveClaudeSettings, referenceFiles);

			if (repoSettings.LogResults)
			{
				await logTaskResult.Log(repository.Path, taskName, result, referenceFiles);
			}

			var outcome = classifyTaskResult.Classify(result);

			await recordRunHistory.Record(
				new RunHistoryEntry
				{
					Repository = repository.Path,
					TaskName = taskName,
					Timestamp = DateTime.Now,
					Success = outcome == TaskOutcome.Done,
				}
			);

			switch (outcome)
			{
				case TaskOutcome.Blocked:
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

					break;

				case TaskOutcome.Failed:
					moveTask.Move(pendingFilePath, failedFolder);
					await generateFailedExplanation.Generate(failedFolder, taskName, result);

					if (repoSettings.Tasks.StopOnFailed)
					{
						logger.Warning(
							"Task {TaskName} failed and StopOnFailed is enabled, stopping repository processing",
							taskName
						);

						return;
					}

					break;

				case TaskOutcome.Done:
					moveTask.Move(pendingFilePath, doneFolder);

					if (repoSettings.Git?.CommitAfterEachTask == true)
					{
						var taskNameWithoutExtension = Path.GetFileNameWithoutExtension(taskFile);
						var commitMessage = $"{repoSettings.Git.CommitMessagePrefix} {taskNameWithoutExtension}";

						var commitResult = await commitChanges.Commit(repository.Path, commitMessage);

						if (repoSettings.LogResults)
						{
							await logTaskResult.Log(repository.Path, taskName, commitResult, referenceFiles);
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
							await logTaskResult.Log(repository.Path, taskName, pushResult, referenceFiles);
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

					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(outcome));
			}
		}
	}
}
