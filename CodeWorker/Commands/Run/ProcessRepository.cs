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
	IBuildTaskFolders buildTaskFolders,
	ICollectReferenceFiles collectReferenceFiles,
	IDiscoverTasks discoverTasks,
	IProcessTask processTask,
	ILogger logger
) : IProcessRepository
{
	public async Task Process(RepositorySettings repository, ClaudeSettings globalClaudeSettings)
	{
		if (!IsRepositoryValid(repository))
		{
			return;
		}

		var repoSettings = await loadRepoSettings.Load(repository.Path);

		if (!IsEnabled(repoSettings, repository.Path))
		{
			return;
		}

		var context = await BuildContext(repository, globalClaudeSettings, repoSettings);

		foreach (var taskFile in discoverTasks.Discover(context.Folders.Todo))
		{
			var decision = await processTask.Run(context, taskFile);

			if (decision == TaskProcessingDecision.Stop)
			{
				return;
			}
		}
	}

	private bool IsRepositoryValid(RepositorySettings repository)
	{
		var validationResult = validateRepository.Validate(repository);

		if (validationResult.IsValid)
		{
			return true;
		}

		logger.Error(
			"Repository {RepositoryPath} is broken — skipping. Errors: {Errors}",
			repository.Path,
			string.Join("; ", validationResult.Errors)
		);

		return false;
	}

	private bool IsEnabled(RepoSettings repoSettings, string repositoryPath)
	{
		if (repoSettings.Enabled)
		{
			return true;
		}

		logger.Information("Repository {RepositoryPath} is disabled, skipping", repositoryPath);

		return false;
	}

	private async Task<TaskExecutionContext> BuildContext(
		RepositorySettings repository,
		ClaudeSettings globalClaudeSettings,
		RepoSettings repoSettings
	)
	{
		var folders = buildTaskFolders.Build(repository.Path, repoSettings.Tasks);
		var referenceFiles = await collectReferenceFiles.Collect(folders.Reference);

		LogReferenceFiles(referenceFiles);

		var claudeSettings = globalClaudeSettings.MergeWith(repoSettings.Claude);

		logger.Information(
			"Claude settings: Model={Model}, MaxTurns={MaxTurns}, SkipPermissions={SkipPermissions}, OutputFormat={OutputFormat}, TimeoutMinutes={TimeoutMinutes}",
			claudeSettings.Model,
			claudeSettings.MaxTurns,
			claudeSettings.SkipPermissions,
			claudeSettings.OutputFormat,
			claudeSettings.TimeoutMinutes
		);

		return new TaskExecutionContext
		{
			Repository = repository,
			RepoSettings = repoSettings,
			ClaudeSettings = claudeSettings,
			Folders = folders,
			ReferenceFiles = referenceFiles,
		};
	}

	private void LogReferenceFiles(List<ReferenceFile> referenceFiles)
	{
		if (referenceFiles.Count == 0)
		{
			return;
		}

		var fileNames = string.Join(", ", referenceFiles.Select(f => f.Name));

		logger.Information("Including {Count} reference file(s): {FileNames}", referenceFiles.Count, fileNames);
	}
}
