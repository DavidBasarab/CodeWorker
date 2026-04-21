using System.Text.Json;
using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.History;
using Serilog;

namespace Testing.FatCat.CodeWorker.History;

public class RecordRunHistoryTests
{
	private readonly IAppendFile appendFile;
	private readonly ILogger logger;
	private readonly RecordRunHistory recordRunHistory;
	private readonly RunHistoryEntry entry;

	public RecordRunHistoryTests()
	{
		appendFile = A.Fake<IAppendFile>();
		logger = A.Fake<ILogger>();

		entry = new RunHistoryEntry
		{
			Repository = @"C:\Projects\my-api",
			TaskName = "01_MyTask.md",
			Timestamp = new DateTime(2026, 4, 20, 2, 15, 0),
			Success = true,
		};

		recordRunHistory = new RecordRunHistory(appendFile, logger);
	}

	[Fact]
	public async Task AppendToRunsJsonlInBaseDirectory()
	{
		await recordRunHistory.Record(entry);

		A.CallTo(() => appendFile.Append(A<string>.That.EndsWith("runs.jsonl"), A<string>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AppendSerializedJsonEndingWithNewline()
	{
		var capturedContent = string.Empty;

		A.CallTo(() => appendFile.Append(A<string>._, A<string>._))
			.Invokes((string _, string content) => capturedContent = content);

		await recordRunHistory.Record(entry);

		capturedContent.Should().EndWith("\n");
	}

	[Fact]
	public async Task AppendLineContainingRepository()
	{
		var capturedContent = string.Empty;

		A.CallTo(() => appendFile.Append(A<string>._, A<string>._))
			.Invokes((string _, string content) => capturedContent = content);

		await recordRunHistory.Record(entry);

		var deserialized = JsonSerializer.Deserialize<RunHistoryEntry>(capturedContent.Trim());

		deserialized.Repository.Should().Be(@"C:\Projects\my-api");
	}

	[Fact]
	public async Task AppendLineContainingTaskName()
	{
		var capturedContent = string.Empty;

		A.CallTo(() => appendFile.Append(A<string>._, A<string>._))
			.Invokes((string _, string content) => capturedContent = content);

		await recordRunHistory.Record(entry);

		var deserialized = JsonSerializer.Deserialize<RunHistoryEntry>(capturedContent.Trim());

		deserialized.TaskName.Should().Be("01_MyTask.md");
	}

	[Fact]
	public async Task AppendLineContainingSuccess()
	{
		var capturedContent = string.Empty;

		A.CallTo(() => appendFile.Append(A<string>._, A<string>._))
			.Invokes((string _, string content) => capturedContent = content);

		await recordRunHistory.Record(entry);

		var deserialized = JsonSerializer.Deserialize<RunHistoryEntry>(capturedContent.Trim());

		deserialized.Success.Should().BeTrue();
	}

	[Fact]
	public async Task AppendLineContainingTimestamp()
	{
		var capturedContent = string.Empty;

		A.CallTo(() => appendFile.Append(A<string>._, A<string>._))
			.Invokes((string _, string content) => capturedContent = content);

		await recordRunHistory.Record(entry);

		var deserialized = JsonSerializer.Deserialize<RunHistoryEntry>(capturedContent.Trim());

		deserialized.Timestamp.Should().Be(new DateTime(2026, 4, 20, 2, 15, 0));
	}
}
