using Serilog;

namespace FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleFailedTaskOutcome(IMoveTask moveTask, IGenerateFailedExplanation generateFailedExplanation, ILogger logger)
	: ITaskOutcomeHandler
{
	public async Task<TaskProcessingDecision> Handle(TaskExecutionContext context, TaskExecution task)
	{
		moveTask.Move(task.PendingFilePath, context.Folders.Failed);

		await generateFailedExplanation.Generate(context.Folders.Failed, task.TaskName, task.Result);

		if (!context.RepoSettings.Tasks.StopOnFailed)
		{
			return TaskProcessingDecision.Continue;
		}

		logger.Warning("Task {TaskName} failed and StopOnFailed is enabled, stopping repository processing", task.TaskName);

		return TaskProcessingDecision.Stop;
	}
}
