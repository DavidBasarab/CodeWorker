using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class LoadRepoSettingsTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly LoadRepoSettings loadRepoSettings;

	private readonly string repoSettingsJson = """
		{
		  "Enabled": true,
		  "LogResults": true,
		  "Git": {
		    "CommitAfterEachTask": true,
		    "PushAfterEachTask": false,
		    "PullBeforeEachTask": true,
		    "CommitMessagePrefix": "🤖",
		    "Branch": "main"
		  },
		  "Claude": {
		    "Model": "claude-sonnet-4-6",
		    "MaxTurns": 10,
		    "SkipPermissions": true,
		    "OutputFormat": "json",
		    "SystemPromptFile": "",
		    "AllowedTools": [],
		    "TimeoutMinutes": 30
		  },
		  "Tasks": {
		    "TodoFolder": "tasks/todo",
		    "DoneFolder": "tasks/done",
		    "ReferenceFolder": "tasks/reference",
		    "PendingFolder": "tasks/pending",
		    "BlockedFolder": "tasks/blocked",
		    "StopOnBlocked": true,
		    "RunPlanningPhase": false
		  }
		}
		""";

	public LoadRepoSettingsTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).Returns(Task.FromResult(repoSettingsJson));

		loadRepoSettings = new LoadRepoSettings(fileSystemTools, logger);
	}

	[Fact]
	public async Task ReadTheSettingsFileFromDefaultPath()
	{
		await loadRepoSettings.Load(@"C:\Projects\my-api");

		A.CallTo(() => fileSystemTools.ReadAllText(@"C:\Projects\my-api\tasks\settings.json")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeserializeEnabled()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.Enabled.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeLogResults()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.LogResults.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeTasksTodoFolder()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.Tasks.TodoFolder.Should().Be("tasks/todo");
	}

	[Fact]
	public async Task DeserializeTasksDoneFolder()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.Tasks.DoneFolder.Should().Be("tasks/done");
	}

	[Fact]
	public async Task DeserializeTasksPendingFolder()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.Tasks.PendingFolder.Should().Be("tasks/pending");
	}

	[Fact]
	public async Task DeserializeTasksBlockedFolder()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.Tasks.BlockedFolder.Should().Be("tasks/blocked");
	}

	[Fact]
	public async Task DeserializeStopOnBlocked()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.Tasks.StopOnBlocked.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeGitSettings()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.Git.CommitAfterEachTask.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeClaudeSettings()
	{
		var result = await loadRepoSettings.Load(@"C:\Projects\my-api");

		result.Claude.Model.Should().Be("claude-sonnet-4-6");
	}
}
