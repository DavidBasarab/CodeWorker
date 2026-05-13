using Serilog;

namespace FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleDoneTaskOutcome(IMoveTask moveTask, ILogger logger) : ITaskOutcomeHandler
{
	public Task<TaskProcessingDecision> Handle(TaskExecutionContext context, TaskExecution task)
	{
		logger.Information(
			"Handling Done outcome for {TaskName}: moving to {Destination}",
			task.TaskName,
			context.Folders.Done
		);

		moveTask.Move(task.PendingFilePath, context.Folders.Done);

		logger.Information("Moved {TaskName} to {Destination}", task.TaskName, context.Folders.Done);

		return Task.FromResult(TaskProcessingDecision.Continue);
	}
}
