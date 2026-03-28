using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Settings;

public class LoadAppSettingsTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly LoadAppSettings loadAppSettings;

	private readonly string settingsJson = """
		{
		  "Repositories": [
		    {
		      "Path": "C:\\Projects\\my-api",
		      "Enabled": true,
		      "SettingsPath": "C:\\Projects\\my-api\\tasks\\settings.json"
		    },
		    {
		      "Path": "C:\\Projects\\my-frontend",
		      "Enabled": true,
		      "SettingsPath": "C:\\Projects\\my-frontend\\tasks\\settings.json"
		    }
		  ],
		  "Git": {
		    "CommitAfterEachTask": true,
		    "PushAfterEachTask": true,
		    "PullBeforeEachTask": true,
		    "CommitMessagePrefix": "bot",
		    "Branch": "main"
		  },
		  "Claude": {
		    "Model": "claude-sonnet-4-6",
		    "MaxTurns": 10,
		    "SkipPermissions": true,
		    "OutputFormat": "json",
		    "SystemPromptFile": "",
		    "AllowedTools": ["Read", "Write"],
		    "TimeoutMinutes": 30
		  }
		}
		""";

	public LoadAppSettingsTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => fileSystemTools.ReadAllText(A<string>.Ignored)).Returns(Task.FromResult(settingsJson));

		loadAppSettings = new LoadAppSettings(fileSystemTools, logger);
	}

	[Fact]
	public async Task ReadTheAppSettingsFile()
	{
		await loadAppSettings.Load();

		A.CallTo(() => fileSystemTools.ReadAllText(A<string>.That.EndsWith("appsettings.json"))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeserializeRepositories()
	{
		var result = await loadAppSettings.Load();

		result.Repositories.Should().HaveCount(2);
	}

	[Fact]
	public async Task DeserializeFirstRepositoryPath()
	{
		var result = await loadAppSettings.Load();

		result.Repositories[0].Path.Should().Be(@"C:\Projects\my-api");
	}

	[Fact]
	public async Task DeserializeFirstRepositoryEnabled()
	{
		var result = await loadAppSettings.Load();

		result.Repositories[0].Enabled.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeFirstRepositorySettingsPath()
	{
		var result = await loadAppSettings.Load();

		result.Repositories[0].SettingsPath.Should().Be(@"C:\Projects\my-api\tasks\settings.json");
	}

	[Fact]
	public async Task DeserializeGitCommitAfterEachTask()
	{
		var result = await loadAppSettings.Load();

		result.Git.CommitAfterEachTask.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeGitPushAfterEachTask()
	{
		var result = await loadAppSettings.Load();

		result.Git.PushAfterEachTask.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeGitPullBeforeEachTask()
	{
		var result = await loadAppSettings.Load();

		result.Git.PullBeforeEachTask.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeGitCommitMessagePrefix()
	{
		var result = await loadAppSettings.Load();

		result.Git.CommitMessagePrefix.Should().Be("bot");
	}

	[Fact]
	public async Task DeserializeGitBranch()
	{
		var result = await loadAppSettings.Load();

		result.Git.Branch.Should().Be("main");
	}

	[Fact]
	public async Task DeserializeClaudeModel()
	{
		var result = await loadAppSettings.Load();

		result.Claude.Model.Should().Be("claude-sonnet-4-6");
	}

	[Fact]
	public async Task DeserializeClaudeMaxTurns()
	{
		var result = await loadAppSettings.Load();

		result.Claude.MaxTurns.Should().Be(10);
	}

	[Fact]
	public async Task DeserializeClaudeSkipPermissions()
	{
		var result = await loadAppSettings.Load();

		result.Claude.SkipPermissions.Should().BeTrue();
	}

	[Fact]
	public async Task DeserializeClaudeOutputFormat()
	{
		var result = await loadAppSettings.Load();

		result.Claude.OutputFormat.Should().Be("json");
	}

	[Fact]
	public async Task DeserializeClaudeAllowedTools()
	{
		var result = await loadAppSettings.Load();

		result.Claude.AllowedTools.Should().BeEquivalentTo(new List<string> { "Read", "Write" });
	}

	[Fact]
	public async Task DeserializeClaudeTimeoutMinutes()
	{
		var result = await loadAppSettings.Load();

		result.Claude.TimeoutMinutes.Should().Be(30);
	}
}
