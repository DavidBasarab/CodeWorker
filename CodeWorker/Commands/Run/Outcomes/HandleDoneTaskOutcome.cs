namespace FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleDoneTaskOutcome(IMoveTask moveTask, IRunGitWorkflow runGitWorkflow) : ITaskOutcomeHandler
{
	public async Task<TaskProcessingDecision> Handle(TaskExecutionContext context, TaskExecution task)
	{
		moveTask.Move(task.PendingFilePath, context.Folders.Done);

		return await runGitWorkflow.Run(context, task);
	}
}
