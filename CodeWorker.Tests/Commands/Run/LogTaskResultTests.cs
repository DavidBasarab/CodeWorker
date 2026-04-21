using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.Process;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class LogTaskResultTests
{
	private readonly IAppendFile appendFile;
	private readonly ILogger logger;
	private readonly LogTaskResult logTaskResult;
	private readonly ProcessResult processResult;
	private readonly List<ReferenceFile> referenceFiles;

	public LogTaskResultTests()
	{
		appendFile = A.Fake<IAppendFile>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => appendFile.Append(A<string>._, A<string>._)).Returns(Task.CompletedTask);

		processResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "Task completed successfully" },
			ErrorLines = new List<string>(),
		};

		referenceFiles = new List<ReferenceFile>();

		logTaskResult = new LogTaskResult(appendFile, logger);
	}

	[Fact]
	public async Task WriteToCodeWorkerLogInTheRepository()
	{
		await logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", processResult, referenceFiles);

		A.CallTo(() => appendFile.Append(@"C:\Projects\my-api\CodeWorker.log", A<string>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheTaskNameInTheLog()
	{
		await logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", processResult, referenceFiles);

		A.CallTo(() => appendFile.Append(A<string>._, A<string>.That.Contains("01_MyTask.md"))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeTheExitCodeInTheLog()
	{
		await logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", processResult, referenceFiles);

		A.CallTo(() => appendFile.Append(A<string>._, A<string>.That.Contains("Exit Code: 0"))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeOutputLinesInTheLog()
	{
		await logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", processResult, referenceFiles);

		A.CallTo(() => appendFile.Append(A<string>._, A<string>.That.Contains("Task completed successfully")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeErrorLinesInTheLogWhenPresent()
	{
		processResult.ErrorLines.Add("Something went wrong");

		await logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", processResult, referenceFiles);

		A.CallTo(() => appendFile.Append(A<string>._, A<string>.That.Contains("Something went wrong")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeReferenceFileNamesInTheLog()
	{
		referenceFiles.Add(new ReferenceFile { Name = "context.md", Content = "some content" });
		referenceFiles.Add(new ReferenceFile { Name = "schema.md", Content = "other content" });

		await logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", processResult, referenceFiles);

		A.CallTo(() => appendFile.Append(A<string>._, A<string>.That.Contains("context.md, schema.md")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeNoneWhenNoReferenceFiles()
	{
		await logTaskResult.Log(@"C:\Projects\my-api", "01_MyTask.md", processResult, referenceFiles);

		A.CallTo(() => appendFile.Append(A<string>._, A<string>.That.Contains("Reference Files: none")))
			.MustHaveHappenedOnceExactly();
	}
}
