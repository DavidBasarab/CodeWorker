using FatCat.CodeWorker.Commands.Info;
using FatCat.CodeWorker.History;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Info;

public class InfoCommandTests
{
	private readonly ILoadRunHistory loadRunHistory;
	private readonly ILogger logger;
	private readonly InfoCommand command;
	private List<RunHistoryEntry> currentHistory;

	public InfoCommandTests()
	{
		loadRunHistory = A.Fake<ILoadRunHistory>();
		logger = A.Fake<ILogger>();

		currentHistory = BuildHistory(15);

		A.CallTo(() => loadRunHistory.Load()).ReturnsLazily(() => Task.FromResult(currentHistory));

		command = new InfoCommand(loadRunHistory, logger);
	}

	private static List<RunHistoryEntry> BuildHistory(int count)
	{
		var baseTime = new DateTime(2026, 4, 20, 2, 0, 0);
		var entries = new List<RunHistoryEntry>();

		for (var i = 0; i < count; i++)
		{
			entries.Add(
				new RunHistoryEntry
				{
					Repository = $@"C:\Projects\repo-{i}",
					TaskName = $"{i:D2}_Task.md",
					Timestamp = baseTime.AddMinutes(i),
					Success = i % 2 == 0,
				}
			);
		}

		return entries;
	}

	[Fact]
	public async Task LoadTheRunHistory()
	{
		await command.Execute(new[] { "info" });

		A.CallTo(() => loadRunHistory.Load()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ShowLastTenEntriesByDefault()
	{
		await command.Execute(new[] { "info" });

		A.CallTo(() => logger.Information(A<string>._, A<DateTime>._, A<string>._, A<string>._, A<string>._))
			.MustHaveHappened(10, Times.Exactly);
	}

	[Fact]
	public async Task ShowRequestedNumberOfEntriesWhenCountProvided()
	{
		await command.Execute(new[] { "info", "5" });

		A.CallTo(() => logger.Information(A<string>._, A<DateTime>._, A<string>._, A<string>._, A<string>._))
			.MustHaveHappened(5, Times.Exactly);
	}

	[Fact]
	public async Task ShowAllEntriesWhenCountExceedsHistorySize()
	{
		currentHistory = BuildHistory(3);

		await command.Execute(new[] { "info", "50" });

		A.CallTo(() => logger.Information(A<string>._, A<DateTime>._, A<string>._, A<string>._, A<string>._))
			.MustHaveHappened(3, Times.Exactly);
	}

	[Fact]
	public async Task ShowMostRecentEntryLast()
	{
		await command.Execute(new[] { "info" });

		A.CallTo(() =>
				logger.Information(
					A<string>._,
					new DateTime(2026, 4, 20, 2, 14, 0),
					A<string>._,
					@"C:\Projects\repo-14",
					"14_Task.md"
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogSuccessStatusForSuccessfulRun()
	{
		currentHistory = new List<RunHistoryEntry>
		{
			new()
			{
				Repository = @"C:\Projects\my-api",
				TaskName = "01_Task.md",
				Timestamp = new DateTime(2026, 4, 20, 2, 0, 0),
				Success = true,
			},
		};

		await command.Execute(new[] { "info" });

		A.CallTo(() => logger.Information(A<string>._, A<DateTime>._, "success", A<string>._, A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogFailureStatusForFailedRun()
	{
		currentHistory = new List<RunHistoryEntry>
		{
			new()
			{
				Repository = @"C:\Projects\my-api",
				TaskName = "01_Task.md",
				Timestamp = new DateTime(2026, 4, 20, 2, 0, 0),
				Success = false,
			},
		};

		await command.Execute(new[] { "info" });

		A.CallTo(() => logger.Information(A<string>._, A<DateTime>._, "failure", A<string>._, A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWhenHistoryIsEmpty()
	{
		currentHistory = new List<RunHistoryEntry>();

		await command.Execute(new[] { "info" });

		A.CallTo(() => logger.Information("No run history found")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IgnoreNonNumericCountArgumentAndUseDefault()
	{
		await command.Execute(new[] { "info", "not-a-number" });

		A.CallTo(() => logger.Information(A<string>._, A<DateTime>._, A<string>._, A<string>._, A<string>._))
			.MustHaveHappened(10, Times.Exactly);
	}
}
