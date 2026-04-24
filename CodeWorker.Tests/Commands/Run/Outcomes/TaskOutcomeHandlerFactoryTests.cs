using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Run.Outcomes;

namespace Testing.FatCat.CodeWorker.Commands.Run.Outcomes;

public class TaskOutcomeHandlerFactoryTests
{
	private readonly HandleDoneTaskOutcome doneHandler;
	private readonly HandleBlockedTaskOutcome blockedHandler;
	private readonly HandleFailedTaskOutcome failedHandler;
	private readonly TaskOutcomeHandlerFactory factory;

	public TaskOutcomeHandlerFactoryTests()
	{
		doneHandler = new HandleDoneTaskOutcome(A.Fake<IMoveTask>(), A.Fake<IRunGitWorkflow>(), A.Fake<Serilog.ILogger>());
		blockedHandler = new HandleBlockedTaskOutcome(
			A.Fake<IMoveTask>(),
			A.Fake<IGenerateBlockedExplanation>(),
			A.Fake<Serilog.ILogger>()
		);
		failedHandler = new HandleFailedTaskOutcome(
			A.Fake<IMoveTask>(),
			A.Fake<IGenerateFailedExplanation>(),
			A.Fake<Serilog.ILogger>()
		);

		factory = new TaskOutcomeHandlerFactory(doneHandler, blockedHandler, failedHandler);
	}

	[Fact]
	public void ReturnDoneHandlerForDoneOutcome()
	{
		factory.For(TaskOutcome.Done).Should().BeSameAs(doneHandler);
	}

	[Fact]
	public void ReturnBlockedHandlerForBlockedOutcome()
	{
		factory.For(TaskOutcome.Blocked).Should().BeSameAs(blockedHandler);
	}

	[Fact]
	public void ReturnFailedHandlerForFailedOutcome()
	{
		factory.For(TaskOutcome.Failed).Should().BeSameAs(failedHandler);
	}

	[Fact]
	public void ThrowForUnknownOutcome()
	{
		var act = () => factory.For((TaskOutcome)999);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}
}
