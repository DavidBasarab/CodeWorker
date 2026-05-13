using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Run.Outcomes;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleBlockedTaskOutcomeTests
{
	private readonly IMoveTask moveTask;
	private readonly IGenerateBlockedExplanation generateBlockedExplanation;
	private readonly ILogger logger;
	private readonly HandleBlockedTaskOutcome handler;
	private readonly TaskExecutionContext context;
	private readonly TaskExecution task;

	public HandleBlockedTaskOutcomeTests()
	{
		moveTask = A.Fake<IMoveTask>();
		generateBlockedExplanation = A.Fake<IGenerateBlockedExplanation>();
		logger = A.Fake<ILogger>();

		context = new TaskExecutionContext
		{
			RepoSettings = new RepoSettings { Tasks = new TaskSettings { StopOnBlocked = false } },
			Folders = new TaskFolders { Blocked = @"C:\Projects\my-api\tasks\blocked" },
		};

		task = new TaskExecution
		{
			TaskName = "01_MyTask.md",
			PendingFilePath = @"C:\Projects\my-api\tasks\pending\01_MyTask.md",
			Result = new ProcessResult { ExitCode = 0 },
		};

		A.CallTo(() => generateBlockedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._))
			.Returns(Task.CompletedTask);

		handler = new HandleBlockedTaskOutcome(moveTask, generateBlockedExplanation, logger);
	}

	[Fact]
	public async Task MoveTaskToBlockedFolder()
	{
		await handler.Handle(context, task);

		A.CallTo(() => moveTask.Move(task.PendingFilePath, context.Folders.Blocked)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateBlockedExplanation()
	{
		await handler.Handle(context, task);

		A.CallTo(() => generateBlockedExplanation.Generate(context.Folders.Blocked, task.TaskName, task.Result))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnStopWhenStopOnBlockedIsTrue()
	{
		context.RepoSettings.Tasks.StopOnBlocked = true;

		var decision = await handler.Handle(context, task);

		decision.Should().Be(TaskProcessingDecision.Stop);
	}

	[Fact]
	public async Task ReturnContinueWhenStopOnBlockedIsFalse()
	{
		context.RepoSettings.Tasks.StopOnBlocked = false;

		var decision = await handler.Handle(context, task);

		decision.Should().Be(TaskProcessingDecision.Continue);
	}

	[Fact]
	public async Task GenerateExplanationBeforeStopping()
	{
		context.RepoSettings.Tasks.StopOnBlocked = true;

		await handler.Handle(context, task);

		A.CallTo(() => generateBlockedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogBeforeMove()
	{
		await handler.Handle(context, task);

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("Handling Blocked outcome"),
					A<string>.That.Contains("01_MyTask.md"),
					A<string>.That.Contains("blocked")
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
					A<string>.That.Contains("blocked")
				)
			)
			.MustHaveHappenedOnceExactly();
	}
}
