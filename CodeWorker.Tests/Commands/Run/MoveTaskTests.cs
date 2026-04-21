using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.FileSystem;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class MoveTaskTests
{
	private readonly IMoveFile moveFile;
	private readonly ILogger logger;
	private readonly MoveTask moveTask;

	public MoveTaskTests()
	{
		moveFile = A.Fake<IMoveFile>();
		logger = A.Fake<ILogger>();

		moveTask = new MoveTask(moveFile, logger);
	}

	[Fact]
	public void MoveTheFileToTheDestinationFolder()
	{
		moveTask.Move(@"C:\tasks\todo\01_MyTask.md", @"C:\tasks\pending");

		A.CallTo(() => moveFile.Move(@"C:\tasks\todo\01_MyTask.md", @"C:\tasks\pending\01_MyTask.md"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void PreserveTheFileName()
	{
		moveTask.Move(@"C:\tasks\todo\SomeTask.md", @"C:\tasks\done");

		A.CallTo(() => moveFile.Move(@"C:\tasks\todo\SomeTask.md", @"C:\tasks\done\SomeTask.md")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void LogTheMoveOperation()
	{
		moveTask.Move(@"C:\tasks\todo\01_MyTask.md", @"C:\tasks\pending");

		A.CallTo(() => logger.Information(A<string>.That.Contains("Moving task"), A<string>._, A<string>._)).MustHaveHappened();
	}
}
