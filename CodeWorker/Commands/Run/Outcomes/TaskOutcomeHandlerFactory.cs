namespace FatCat.CodeWorker.Commands.Run.Outcomes;

public interface ITaskOutcomeHandlerFactory
{
	ITaskOutcomeHandler For(TaskOutcome outcome);
}

public class TaskOutcomeHandlerFactory(
	HandleDoneTaskOutcome doneHandler,
	HandleBlockedTaskOutcome blockedHandler,
	HandleFailedTaskOutcome failedHandler
) : ITaskOutcomeHandlerFactory
{
	public ITaskOutcomeHandler For(TaskOutcome outcome)
	{
		return outcome switch
		{
			TaskOutcome.Done => doneHandler,
			TaskOutcome.Blocked => blockedHandler,
			TaskOutcome.Failed => failedHandler,
			_ => throw new ArgumentOutOfRangeException(nameof(outcome)),
		};
	}
}
