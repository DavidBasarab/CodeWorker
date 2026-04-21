using FatCat.CodeWorker.Git;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IRunGitWorkflow
{
	Task<TaskProcessingDecision> Run(TaskExecutionContext context, TaskExecution task);
}

public class RunGitWorkflow(
	ICommitChanges commitChanges,
	IPushChanges pushChanges,
	ILogTaskResult logTaskResult,
	ILogger logger
) : IRunGitWorkflow
{
	private TaskExecutionContext context;
	private TaskExecution task;

	public async Task<TaskProcessingDecision> Run(TaskExecutionContext executionContext, TaskExecution currentTask)
	{
		context = executionContext;
		task = currentTask;

		var commitDecision = await RunCommitIfConfigured();

		if (commitDecision == TaskProcessingDecision.Stop)
		{
			return TaskProcessingDecision.Stop;
		}

		return await RunPushIfConfigured();
	}

	private async Task<TaskProcessingDecision> RunCommitIfConfigured()
	{
		if (context.RepoSettings.Git?.CommitAfterEachTask != true)
		{
			return TaskProcessingDecision.Continue;
		}

		var taskNameWithoutExtension = Path.GetFileNameWithoutExtension(task.TaskFile);
		var commitMessage = $"{context.RepoSettings.Git.CommitMessagePrefix} {taskNameWithoutExtension}";

		var commitResult = await commitChanges.Commit(context.Repository.Path, commitMessage);

		await LogResultIfEnabled(commitResult);

		if (commitResult.ExitCode != 0)
		{
			logger.Error("Commit failed for task {TaskName}, stopping repository processing", task.TaskName);

			return TaskProcessingDecision.Stop;
		}

		return TaskProcessingDecision.Continue;
	}

	private async Task<TaskProcessingDecision> RunPushIfConfigured()
	{
		if (context.RepoSettings.Git?.PushAfterEachTask != true)
		{
			return TaskProcessingDecision.Continue;
		}

		var pushResult = await pushChanges.Push(context.Repository.Path);

		await LogResultIfEnabled(pushResult);

		if (pushResult.ExitCode != 0)
		{
			logger.Error(
				"Push failed for task {TaskName}, possible merge conflict — stopping repository processing",
				task.TaskName
			);

			return TaskProcessingDecision.Stop;
		}

		return TaskProcessingDecision.Continue;
	}

	private async Task LogResultIfEnabled(Process.ProcessResult result)
	{
		if (!context.RepoSettings.LogResults)
		{
			return;
		}

		await logTaskResult.Log(context.Repository.Path, task.TaskName, result, context.ReferenceFiles);
	}
}
