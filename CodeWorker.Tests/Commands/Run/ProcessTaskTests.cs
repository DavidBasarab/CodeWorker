using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Run.Outcomes;
using FatCat.CodeWorker.History;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class ProcessTaskTests
{
	private readonly IMoveTask moveTask;
	private readonly IRunClaude runClaude;
	private readonly ILogTaskResult logTaskResult;
	private readonly IClassifyTaskResult classifyTaskResult;
	private readonly IRecordRunHistory recordRunHistory;
	private readonly ITaskOutcomeHandlerFactory outcomeHandlerFactory;
	private readonly ITaskOutcomeHandler outcomeHandler;
	private readonly ILogger logger;
	private readonly ProcessTask processTask;
	private readonly TaskExecutionContext context;
	private readonly string taskFile;
	private ProcessResult claudeResult;
	private TaskOutcome currentOutcome;
	private TaskProcessingDecision outcomeDecision;

	public ProcessTaskTests()
	{
		moveTask = A.Fake<IMoveTask>();
		runClaude = A.Fake<IRunClaude>();
		logTaskResult = A.Fake<ILogTaskResult>();
		classifyTaskResult = A.Fake<IClassifyTaskResult>();
		recordRunHistory = A.Fake<IRecordRunHistory>();
		outcomeHandlerFactory = A.Fake<ITaskOutcomeHandlerFactory>();
		outcomeHandler = A.Fake<ITaskOutcomeHandler>();
		logger = A.Fake<ILogger>();

		taskFile = @"C:\Projects\my-api\tasks\todo\01_MyTask.md";

		claudeResult = new ProcessResult { ExitCode = 0 };
		currentOutcome = TaskOutcome.Done;
		outcomeDecision = TaskProcessingDecision.Continue;

		context = new TaskExecutionContext
		{
			Repository = new RepositorySettings { Path = @"C:\Projects\my-api" },
			RepoSettings = new RepoSettings { LogResults = true, Tasks = new TaskSettings() },
			ClaudeSettings = new ClaudeSettings { Model = "claude-opus-4-6" },
			Folders = new TaskFolders
			{
				Todo = @"C:\Projects\my-api\tasks\todo",
				Pending = @"C:\Projects\my-api\tasks\pending",
				Done = @"C:\Projects\my-api\tasks\done",
				Blocked = @"C:\Projects\my-api\tasks\blocked",
				Failed = @"C:\Projects\my-api\tasks\failed",
				Reference = @"C:\Projects\my-api\tasks\reference",
			},
			ReferenceFiles = new List<ReferenceFile>(),
		};

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._))
			.ReturnsLazily(() => Task.FromResult(claudeResult));
		A.CallTo(() => classifyTaskResult.Classify(A<ProcessResult>._)).ReturnsLazily(() => currentOutcome);
		A.CallTo(() => logTaskResult.Log(A<string>._, A<string>._, A<ProcessResult>._, A<List<ReferenceFile>>._))
			.Returns(Task.CompletedTask);
		A.CallTo(() => recordRunHistory.Record(A<RunHistoryEntry>._)).Returns(Task.CompletedTask);
		A.CallTo(() => outcomeHandlerFactory.For(A<TaskOutcome>._)).Returns(outcomeHandler);
		A.CallTo(() => outcomeHandler.Handle(A<TaskExecutionContext>._, A<TaskExecution>._))
			.ReturnsLazily(() => Task.FromResult(outcomeDecision));

		processTask = new ProcessTask(
			moveTask,
			runClaude,
			logTaskResult,
			classifyTaskResult,
			recordRunHistory,
			outcomeHandlerFactory,
			logger
		);
	}

	[Fact]
	public async Task LogStartingTask()
	{
		await processTask.Run(context, taskFile);

		A.CallTo(() => logger.Information(A<string>.That.Contains("Starting task"), A<string>.That.Contains("01_MyTask.md")))
			.MustHaveHappened();
	}

	[Fact]
	public async Task MoveTaskFromTodoToPending()
	{
		await processTask.Run(context, taskFile);

		A.CallTo(() => moveTask.Move(taskFile, context.Folders.Pending)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RunClaudeWithPendingFilePath()
	{
		await processTask.Run(context, taskFile);

		var expectedPendingPath = Path.Combine(context.Folders.Pending, "01_MyTask.md");

		A.CallTo(() => runClaude.Run(expectedPendingPath, context.ClaudeSettings, context.ReferenceFiles))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogResultWhenLogResultsIsEnabled()
	{
		await processTask.Run(context, taskFile);

		A.CallTo(() => logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", claudeResult, context.ReferenceFiles))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipLoggingResultWhenLogResultsIsDisabled()
	{
		context.RepoSettings.LogResults = false;

		await processTask.Run(context, taskFile);

		A.CallTo(() => logTaskResult.Log(A<string>._, A<string>._, A<ProcessResult>._, A<List<ReferenceFile>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ClassifyTaskResult()
	{
		await processTask.Run(context, taskFile);

		A.CallTo(() => classifyTaskResult.Classify(claudeResult)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordSuccessInRunHistoryWhenOutcomeIsDone()
	{
		currentOutcome = TaskOutcome.Done;

		await processTask.Run(context, taskFile);

		A.CallTo(() =>
				recordRunHistory.Record(
					A<RunHistoryEntry>.That.Matches(entry =>
						entry.Repository == @"C:\Projects\my-api" && entry.TaskName == "01_MyTask.md" && entry.Success == true
					)
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailureInRunHistoryWhenOutcomeIsFailed()
	{
		currentOutcome = TaskOutcome.Failed;

		await processTask.Run(context, taskFile);

		A.CallTo(() => recordRunHistory.Record(A<RunHistoryEntry>.That.Matches(entry => entry.Success == false)))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchToOutcomeHandlerForClassifiedOutcome()
	{
		currentOutcome = TaskOutcome.Blocked;

		await processTask.Run(context, taskFile);

		A.CallTo(() => outcomeHandlerFactory.For(TaskOutcome.Blocked)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeOutcomeHandler()
	{
		await processTask.Run(context, taskFile);

		A.CallTo(() => outcomeHandler.Handle(context, A<TaskExecution>.That.Matches(t => t.TaskName == "01_MyTask.md")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnOutcomeHandlerDecision()
	{
		outcomeDecision = TaskProcessingDecision.Stop;

		var decision = await processTask.Run(context, taskFile);

		decision.Should().Be(TaskProcessingDecision.Stop);
	}

	[Fact]
	public async Task RethrowExceptionsFromMoveTask()
	{
		var expectedException = new InvalidOperationException("move failed");

		A.CallTo(() => moveTask.Move(A<string>._, A<string>._)).Throws(expectedException);

		var act = async () => await processTask.Run(context, taskFile);

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("move failed");
	}

	[Fact]
	public async Task LogErrorWhenExceptionIsThrown()
	{
		var expectedException = new InvalidOperationException("move failed");

		A.CallTo(() => moveTask.Move(A<string>._, A<string>._)).Throws(expectedException);

		var act = async () => await processTask.Run(context, taskFile);

		await act.Should().ThrowAsync<InvalidOperationException>();

		A.CallTo(() =>
				logger.Error(
					expectedException,
					A<string>.That.Contains("Unhandled exception"),
					A<string>.That.Contains("01_MyTask.md")
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RethrowExceptionsFromRunClaude()
	{
		var expectedException = new InvalidOperationException("claude failed");

		A.CallTo(() => runClaude.Run(A<string>._, A<ClaudeSettings>._, A<List<ReferenceFile>>._)).Throws(expectedException);

		var act = async () => await processTask.Run(context, taskFile);

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("claude failed");
	}

	[Fact]
	public async Task RethrowExceptionsFromClassifyTaskResult()
	{
		var expectedException = new InvalidOperationException("classify failed");

		A.CallTo(() => classifyTaskResult.Classify(A<ProcessResult>._)).Throws(expectedException);

		var act = async () => await processTask.Run(context, taskFile);

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("classify failed");
	}

	[Fact]
	public async Task RethrowExceptionsFromOutcomeHandler()
	{
		var expectedException = new InvalidOperationException("handler failed");

		A.CallTo(() => outcomeHandler.Handle(A<TaskExecutionContext>._, A<TaskExecution>._)).Throws(expectedException);

		var act = async () => await processTask.Run(context, taskFile);

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("handler failed");
	}

	[Fact]
	public async Task InvokeOutcomeHandlerOnlyAfterHistoryIsRecorded()
	{
		await processTask.Run(context, taskFile);

		A.CallTo(() => recordRunHistory.Record(A<RunHistoryEntry>._))
			.MustHaveHappenedOnceExactly()
			.Then(
				A.CallTo(() => outcomeHandler.Handle(A<TaskExecutionContext>._, A<TaskExecution>._))
					.MustHaveHappenedOnceExactly()
			);
	}
}
