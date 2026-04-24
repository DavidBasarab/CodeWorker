using Serilog;

namespace FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleBlockedTaskOutcome(
	IMoveTask moveTask,
	IGenerateBlockedExplanation generateBlockedExplanation,
	ILogger logger
) : ITaskOutcomeHandler
{
	public async Task<TaskProcessingDecision> Handle(TaskExecutionContext context, TaskExecution task)
	{
		logger.Information(
			"Handling Blocked outcome for {TaskName}: moving to {Destination}",
			task.TaskName,
			context.Folders.Blocked
		);

		moveTask.Move(task.PendingFilePath, context.Folders.Blocked);

		logger.Information("Moved {TaskName} to {Destination}", task.TaskName, context.Folders.Blocked);

		await generateBlockedExplanation.Generate(context.Folders.Blocked, task.TaskName, task.Result);

		if (!context.RepoSettings.Tasks.StopOnBlocked)
		{
			return TaskProcessingDecision.Continue;
		}

		logger.Warning(
			"Task {TaskName} was blocked and StopOnBlocked is enabled, stopping repository processing",
			task.TaskName
		);

		return TaskProcessingDecision.Stop;
	}
}
