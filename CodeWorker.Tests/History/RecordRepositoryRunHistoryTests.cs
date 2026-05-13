using System.Text.Json;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.History;
using Serilog;

namespace Testing.FatCat.CodeWorker.History;

public class RecordRepositoryRunHistoryTests
{
	private readonly IAppendFile appendFile;
	private readonly ILogger logger;
	private readonly RecordRepositoryRunHistory recordRepositoryRunHistory;
	private readonly RepositoryRunHistoryEntry entry;
	private readonly string repositoryPath;
	private string capturedContent;

	public RecordRepositoryRunHistoryTests()
	{
		appendFile = A.Fake<IAppendFile>();
		logger = A.Fake<ILogger>();

		repositoryPath = @"C:\Projects\my-api";

		entry = new RepositoryRunHistoryEntry
		{
			Timestamp = new DateTime(2026, 4, 20, 2, 15, 0),
			TaskName = "05-refactor-run-process.md",
			Outcome = TaskOutcome.Done,
			ExitCode = 0,
			DurationMs = 42000,
			CommitHash = "abc123",
		};

		capturedContent = string.Empty;

		A.CallTo(() => appendFile.Append(A<string>._, A<string>._))
			.Invokes((string _, string content) => capturedContent = content)
			.Returns(Task.CompletedTask);

		recordRepositoryRunHistory = new RecordRepositoryRunHistory(appendFile, logger);
	}

	[Fact]
	public async Task WriteToTheRepositoryTasksFolder()
	{
		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		A.CallTo(() => appendFile.Append(@"C:\Projects\my-api\tasks\run-history.jsonl", A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AppendOneJsonLinePerEntry()
	{
		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		capturedContent.Should().EndWith("\n");
		capturedContent.Trim().Should().NotContain("\n");
	}

	[Fact]
	public async Task IncludeTheTaskName()
	{
		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		capturedContent.Should().Contain("05-refactor-run-process.md");
	}

	[Fact]
	public async Task IncludeTheOutcome()
	{
		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		capturedContent.Should().Contain("Done");
	}

	[Fact]
	public async Task IncludeTheTimestamp()
	{
		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		capturedContent.Should().Contain("2026");
	}

	[Fact]
	public async Task IncludeTheExitCode()
	{
		entry.ExitCode = 42;

		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		capturedContent.Should().Contain("42");
	}

	[Fact]
	public async Task IncludeTheDurationMs()
	{
		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		capturedContent.Should().Contain("42000");
	}

	[Fact]
	public async Task IncludeTheCommitHashWhenProvided()
	{
		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		capturedContent.Should().Contain("abc123");
	}

	[Fact]
	public async Task SerializeNullCommitHashAsNull()
	{
		entry.CommitHash = null;

		await recordRepositoryRunHistory.Record(repositoryPath, entry);

		capturedContent.Should().Contain("\"commitHash\":null");
	}
}
