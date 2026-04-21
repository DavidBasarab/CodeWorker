using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run.Outcomes;
using FatCat.CodeWorker.History;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IProcessTask
{
	Task<TaskProcessingDecision> Run(TaskExecutionContext context, string taskFile);
}

public class ProcessTask(
	IMoveTask moveTask,
	IRunClaude runClaude,
	ILogTaskResult logTaskResult,
	IClassifyTaskResult classifyTaskResult,
	IRecordRunHistory recordRunHistory,
	ITaskOutcomeHandlerFactory outcomeHandlerFactory,
	ILogger logger
) : IProcessTask
{
	private TaskExecutionContext context;
	private TaskExecution task;

	public async Task<TaskProcessingDecision> Run(TaskExecutionContext executionContext, string taskFile)
	{
		context = executionContext;
		task = new TaskExecution
		{
			TaskFile = taskFile,
			TaskName = Path.GetFileName(taskFile),
			PendingFilePath = Path.Combine(context.Folders.Pending, Path.GetFileName(taskFile)),
		};

		logger.Information("Starting task {TaskName}", task.TaskName);

		moveTask.Move(task.TaskFile, context.Folders.Pending);

		task.Result = await runClaude.Run(task.PendingFilePath, context.ClaudeSettings, context.ReferenceFiles);

		await LogResultIfEnabled();

		var outcome = classifyTaskResult.Classify(task.Result);

		await RecordHistory(outcome);

		return await outcomeHandlerFactory.For(outcome).Handle(context, task);
	}

	private async Task LogResultIfEnabled()
	{
		if (!context.RepoSettings.LogResults)
		{
			return;
		}

		await logTaskResult.Log(context.Repository.Path, task.TaskName, task.Result, context.ReferenceFiles);
	}

	private async Task RecordHistory(TaskOutcome outcome)
	{
		await recordRunHistory.Record(
			new RunHistoryEntry
			{
				Repository = context.Repository.Path,
				TaskName = task.TaskName,
				Timestamp = DateTime.Now,
				Success = outcome == TaskOutcome.Done,
			}
		);
	}
}
