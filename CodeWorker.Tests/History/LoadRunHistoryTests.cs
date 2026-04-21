using FatCat.CodeWorker.History;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.History;

public class LoadRunHistoryTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly LoadRunHistory loadRunHistory;

	private readonly List<string> historyLines = new()
	{
		"""{"Repository":"C:\\Projects\\my-api","TaskName":"01_First.md","Timestamp":"2026-04-20T02:15:00","Success":true}""",
		"""{"Repository":"C:\\Projects\\my-api","TaskName":"02_Second.md","Timestamp":"2026-04-20T02:20:00","Success":false}""",
	};

	public LoadRunHistoryTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).Returns(true);
		A.CallTo(() => fileSystemTools.ReadAllLines(A<string>._)).Returns(Task.FromResult(historyLines));

		loadRunHistory = new LoadRunHistory(fileSystemTools, logger);
	}

	[Fact]
	public async Task ReadRunsJsonlFromBaseDirectory()
	{
		await loadRunHistory.Load();

		A.CallTo(() => fileSystemTools.ReadAllLines(A<string>.That.EndsWith("runs.jsonl"))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnEmptyListWhenFileDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).Returns(false);

		var result = await loadRunHistory.Load();

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task ReturnOneEntryPerLine()
	{
		var result = await loadRunHistory.Load();

		result.Should().HaveCount(2);
	}

	[Fact]
	public async Task DeserializeRepository()
	{
		var result = await loadRunHistory.Load();

		result[0].Repository.Should().Be(@"C:\Projects\my-api");
	}

	[Fact]
	public async Task DeserializeTaskName()
	{
		var result = await loadRunHistory.Load();

		result[0].TaskName.Should().Be("01_First.md");
	}

	[Fact]
	public async Task DeserializeSuccess()
	{
		var result = await loadRunHistory.Load();

		result[1].Success.Should().BeFalse();
	}

	[Fact]
	public async Task DeserializeTimestamp()
	{
		var result = await loadRunHistory.Load();

		result[0].Timestamp.Should().Be(new DateTime(2026, 4, 20, 2, 15, 0));
	}

	[Fact]
	public async Task SkipBlankLines()
	{
		var linesWithBlanks = new List<string>
		{
			"""{"Repository":"C:\\Projects\\my-api","TaskName":"01_First.md","Timestamp":"2026-04-20T02:15:00","Success":true}""",
			"",
			"   ",
		};

		A.CallTo(() => fileSystemTools.ReadAllLines(A<string>._)).Returns(Task.FromResult(linesWithBlanks));

		var result = await loadRunHistory.Load();

		result.Should().HaveCount(1);
	}
}
