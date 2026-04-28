using System.Diagnostics;
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
	IWriteTaskLog writeTaskLog,
	IClassifyTaskResult classifyTaskResult,
	IRecordRunHistory recordRunHistory,
	IRecordRepositoryRunHistory recordRepositoryRunHistory,
	ITaskOutcomeHandlerFactory outcomeHandlerFactory,
	ILogger logger
) : IProcessTask
{
	private TaskExecutionContext context;
	private TaskExecution task;
	private long durationMs;

	public async Task<TaskProcessingDecision> Run(TaskExecutionContext executionContext, string taskFile)
	{
		context = executionContext;
		task = new TaskExecution
		{
			TaskFile = taskFile,
			TaskName = Path.GetFileName(taskFile),
			PendingFilePath = Path.Combine(context.Folders.Pending, Path.GetFileName(taskFile)),
		};

		try
		{
			logger.Information("Starting task {TaskName}", task.TaskName);

			logger.Information("Moving task {TaskName} to pending", task.TaskName);
			moveTask.Move(task.TaskFile, context.Folders.Pending);
			logger.Information("Task {TaskName} moved to pending", task.TaskName);

			logger.Information("Invoking Claude for {TaskName}", task.TaskName);
			var stopwatch = Stopwatch.StartNew();
			task.Result = await runClaude.Run(task.PendingFilePath, context.ClaudeSettings, context.ReferenceFiles);
			stopwatch.Stop();
			durationMs = stopwatch.ElapsedMilliseconds;
			logger.Information(
				"Claude run returned for {TaskName}: ExitCode={ExitCode}, TimedOut={TimedOut}, FailedToStart={FailedToStart}, OutputLines={OutputLineCount}, ErrorLines={ErrorLineCount}",
				task.TaskName,
				task.Result.ExitCode,
				task.Result.TimedOut,
				task.Result.FailedToStart,
				task.Result.OutputLines.Count,
				task.Result.ErrorLines.Count
			);

			await LogResultIfEnabled();

			var outcome = classifyTaskResult.Classify(task.Result);
			logger.Information("Classified task {TaskName} as {Outcome}", task.TaskName, outcome);

			await WriteTaskLogIfEnabled(outcome);

			await RecordHistory(outcome);
			await RecordRepositoryHistory(outcome);
			logger.Information("Recorded run history for {TaskName}", task.TaskName);

			logger.Information("Invoking outcome handler for {Outcome} on {TaskName}", outcome, task.TaskName);
			var decision = await outcomeHandlerFactory.For(outcome).Handle(context, task);
			logger.Information("Outcome handler complete for {TaskName}", task.TaskName);

			return decision;
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Unhandled exception processing task {TaskName}", task.TaskName);

			throw;
		}
	}

	private async Task WriteTaskLogIfEnabled(TaskOutcome outcome)
	{
		if (!context.RepoSettings.LogResults)
		{
			return;
		}

		await writeTaskLog.Write(context, task, outcome);
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

	private async Task RecordRepositoryHistory(TaskOutcome outcome)
	{
		await recordRepositoryRunHistory.Record(
			context.Repository.Path,
			new RepositoryRunHistoryEntry
			{
				Timestamp = DateTime.Now,
				TaskName = task.TaskName,
				Outcome = outcome,
				ExitCode = task.Result.ExitCode,
				DurationMs = durationMs,
			}
		);
	}
}
