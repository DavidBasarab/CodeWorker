using Serilog;

namespace FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleFailedTaskOutcome(IMoveTask moveTask, IGenerateFailedExplanation generateFailedExplanation, ILogger logger)
	: ITaskOutcomeHandler
{
	public async Task<TaskProcessingDecision> Handle(TaskExecutionContext context, TaskExecution task)
	{
		logger.Information(
			"Handling Failed outcome for {TaskName}: moving to {Destination}",
			task.TaskName,
			context.Folders.Failed
		);

		moveTask.Move(task.PendingFilePath, context.Folders.Failed);

		logger.Information("Moved {TaskName} to {Destination}", task.TaskName, context.Folders.Failed);

		await generateFailedExplanation.Generate(context.Folders.Failed, task.TaskName, task.Result);

		if (!context.RepoSettings.Tasks.StopOnFailed)
		{
			return TaskProcessingDecision.Continue;
		}

		logger.Warning("Task {TaskName} failed and StopOnFailed is enabled, stopping repository processing", task.TaskName);

		return TaskProcessingDecision.Stop;
	}
}
