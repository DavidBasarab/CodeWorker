using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Run.Outcomes;
using FatCat.CodeWorker.Settings;

namespace Testing.FatCat.CodeWorker.Commands.Run.Outcomes;

public class HandleDoneTaskOutcomeTests
{
	private readonly IMoveTask moveTask;
	private readonly IRunGitWorkflow runGitWorkflow;
	private readonly HandleDoneTaskOutcome handler;
	private readonly TaskExecutionContext context;
	private readonly TaskExecution task;
	private TaskProcessingDecision gitDecision;

	public HandleDoneTaskOutcomeTests()
	{
		moveTask = A.Fake<IMoveTask>();
		runGitWorkflow = A.Fake<IRunGitWorkflow>();

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

		gitDecision = TaskProcessingDecision.Continue;

		A.CallTo(() => runGitWorkflow.Run(A<TaskExecutionContext>._, A<TaskExecution>._))
			.ReturnsLazily(() => Task.FromResult(gitDecision));

		handler = new HandleDoneTaskOutcome(moveTask, runGitWorkflow);
	}

	[Fact]
	public async Task MoveTaskToDoneFolder()
	{
		await handler.Handle(context, task);

		A.CallTo(() => moveTask.Move(task.PendingFilePath, context.Folders.Done)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RunGitWorkflow()
	{
		await handler.Handle(context, task);

		A.CallTo(() => runGitWorkflow.Run(context, task)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnGitWorkflowDecision()
	{
		gitDecision = TaskProcessingDecision.Stop;

		var decision = await handler.Handle(context, task);

		decision.Should().Be(TaskProcessingDecision.Stop);
	}
}
