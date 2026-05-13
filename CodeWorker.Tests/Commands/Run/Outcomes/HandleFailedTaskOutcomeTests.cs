using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Run.Outcomes;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleFailedTaskOutcomeTests
{
	private readonly IMoveTask moveTask;
	private readonly IGenerateFailedExplanation generateFailedExplanation;
	private readonly ILogger logger;
	private readonly HandleFailedTaskOutcome handler;
	private readonly TaskExecutionContext context;
	private readonly TaskExecution task;

	public HandleFailedTaskOutcomeTests()
	{
		moveTask = A.Fake<IMoveTask>();
		generateFailedExplanation = A.Fake<IGenerateFailedExplanation>();
		logger = A.Fake<ILogger>();

		context = new TaskExecutionContext
		{
			RepoSettings = new RepoSettings { Tasks = new TaskSettings { StopOnFailed = false } },
			Folders = new TaskFolders { Failed = @"C:\Projects\my-api\tasks\failed" },
		};

		task = new TaskExecution
		{
			TaskName = "01_MyTask.md",
			PendingFilePath = @"C:\Projects\my-api\tasks\pending\01_MyTask.md",
			Result = new ProcessResult { ExitCode = 1 },
		};

		A.CallTo(() => generateFailedExplanation.Generate(A<string>._, A<string>._, A<ProcessResult>._))
			.Returns(Task.CompletedTask);

		handler = new HandleFailedTaskOutcome(moveTask, generateFailedExplanation, logger);
	}

	[Fact]
	public async Task MoveTaskToFailedFolder()
	{
		await handler.Handle(context, task);

		A.CallTo(() => moveTask.Move(task.PendingFilePath, context.Folders.Failed)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateFailedExplanation()
	{
		await handler.Handle(context, task);

		A.CallTo(() => generateFailedExplanation.Generate(context.Folders.Failed, task.TaskName, task.Result))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnStopWhenStopOnFailedIsTrue()
	{
		context.RepoSettings.Tasks.StopOnFailed = true;

		var decision = await handler.Handle(context, task);

		decision.Should().Be(TaskProcessingDecision.Stop);
	}

	[Fact]
	public async Task ReturnContinueWhenStopOnFailedIsFalse()
	{
		context.RepoSettings.Tasks.StopOnFailed = false;

		var decision = await handler.Handle(context, task);

		decision.Should().Be(TaskProcessingDecision.Continue);
	}

	[Fact]
	public async Task LogBeforeMove()
	{
		await handler.Handle(context, task);

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("Handling Failed outcome"),
					A<string>.That.Contains("01_MyTask.md"),
					A<string>.That.Contains("failed")
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
					A<string>.That.Contains("failed")
				)
			)
			.MustHaveHappenedOnceExactly();
	}
}
