namespace FatCat.CodeWorker.Commands.Run.Outcomes;

public interface ITaskOutcomeHandler
{
	Task<TaskProcessingDecision> Handle(TaskExecutionContext context, TaskExecution task);
}
