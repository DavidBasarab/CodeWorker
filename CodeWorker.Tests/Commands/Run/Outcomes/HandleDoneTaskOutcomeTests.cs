using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Run.Outcomes;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleDoneTaskOutcomeTests
{
	private readonly IMoveTask moveTask;
	private readonly ILogger logger;
	private readonly HandleDoneTaskOutcome handler;
	private readonly TaskExecutionContext context;
	private readonly TaskExecution task;

	public HandleDoneTaskOutcomeTests()
	{
		moveTask = A.Fake<IMoveTask>();
		logger = A.Fake<ILogger>();

		context = new TaskExecutionContext
		{
			Repository = new RepositorySettings { Path = @"C:\Projects\my-api" },
			RepoSettings = new RepoSettings(),
			Folders = new TaskFolders { Done = @"C:\Projects\my-api\tasks\done" },
		};

		task = new TaskExecution
		{
			TaskName = "01_MyTask.md",
			PendingFilePath = @"C:\Projects\my-api\tasks\pending\01_MyTask.md",
		};

		handler = new HandleDoneTaskOutcome(moveTask, logger);
	}

	[Fact]
	public async Task MoveTaskToDoneFolder()
	{
		await handler.Handle(context, task);

		A.CallTo(() => moveTask.Move(task.PendingFilePath, context.Folders.Done)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnContinueDecision()
	{
		var decision = await handler.Handle(context, task);

		decision.Should().Be(TaskProcessingDecision.Continue);
	}

	[Fact]
	public async Task LogBeforeMove()
	{
		await handler.Handle(context, task);

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("Handling Done outcome"),
					A<string>.That.Contains("01_MyTask.md"),
					A<string>.That.Contains("done")
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogAfterMove()
	{
		await handler.Handle(context, task);

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("Moved"),
					A<string>.That.Contains("01_MyTask.md"),
					A<string>.That.Contains("done")
				)
			)
			.MustHaveHappenedOnceExactly();
	}
}
