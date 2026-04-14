using FatCat.CodeWorker.Settings;

namespace Testing.FatCat.CodeWorker.Settings;

public class ClaudeSettingsMergeTests
{
	private readonly ClaudeSettings globalSettings;

	public ClaudeSettingsMergeTests()
	{
		globalSettings = new ClaudeSettings
		{
			Model = "claude-opus-4-6",
			MaxTurns = 10,
			SkipPermissions = true,
			OutputFormat = "json",
			SystemPromptFile = "global-prompt.md",
			AllowedTools = new List<string> { "Read", "Write" },
			TimeoutMinutes = 30,
		};
	}

	[Fact]
	public void ReturnGlobalValuesWhenOverrideIsNull()
	{
		var result = globalSettings.MergeWith(null);

		result.Model.Should().Be("claude-opus-4-6");
		result.MaxTurns.Should().Be(10);
		result.SkipPermissions.Should().BeTrue();
		result.OutputFormat.Should().Be("json");
		result.SystemPromptFile.Should().Be("global-prompt.md");
		result.AllowedTools.Should().BeEquivalentTo(new List<string> { "Read", "Write" });
		result.TimeoutMinutes.Should().Be(30);
	}

	[Fact]
	public void OverrideModelWhenRepoSpecifiesIt()
	{
		var repoSettings = new ClaudeSettings { Model = "claude-sonnet-4-6" };

		var result = globalSettings.MergeWith(repoSettings);

		result.Model.Should().Be("claude-sonnet-4-6");
	}

	[Fact]
	public void KeepGlobalModelWhenRepoModelIsEmpty()
	{
		var repoSettings = new ClaudeSettings { Model = "" };

		var result = globalSettings.MergeWith(repoSettings);

		result.Model.Should().Be("claude-opus-4-6");
	}

	[Fact]
	public void KeepGlobalModelWhenRepoModelIsNull()
	{
		var repoSettings = new ClaudeSettings { Model = null };

		var result = globalSettings.MergeWith(repoSettings);

		result.Model.Should().Be("claude-opus-4-6");
	}

	[Fact]
	public void OverrideMaxTurnsWhenRepoSpecifiesIt()
	{
		var repoSettings = new ClaudeSettings { MaxTurns = 25 };

		var result = globalSettings.MergeWith(repoSettings);

		result.MaxTurns.Should().Be(25);
	}

	[Fact]
	public void KeepGlobalMaxTurnsWhenRepoMaxTurnsIsZero()
	{
		var repoSettings = new ClaudeSettings { MaxTurns = 0 };

		var result = globalSettings.MergeWith(repoSettings);

		result.MaxTurns.Should().Be(10);
	}

	[Fact]
	public void OverrideOutputFormatWhenRepoSpecifiesIt()
	{
		var repoSettings = new ClaudeSettings { OutputFormat = "text" };

		var result = globalSettings.MergeWith(repoSettings);

		result.OutputFormat.Should().Be("text");
	}

	[Fact]
	public void KeepGlobalOutputFormatWhenRepoOutputFormatIsEmpty()
	{
		var repoSettings = new ClaudeSettings { OutputFormat = "" };

		var result = globalSettings.MergeWith(repoSettings);

		result.OutputFormat.Should().Be("json");
	}

	[Fact]
	public void OverrideSystemPromptFileWhenRepoSpecifiesIt()
	{
		var repoSettings = new ClaudeSettings { SystemPromptFile = "repo-prompt.md" };

		var result = globalSettings.MergeWith(repoSettings);

		result.SystemPromptFile.Should().Be("repo-prompt.md");
	}

	[Fact]
	public void KeepGlobalSystemPromptFileWhenRepoIsEmpty()
	{
		var repoSettings = new ClaudeSettings { SystemPromptFile = "" };

		var result = globalSettings.MergeWith(repoSettings);

		result.SystemPromptFile.Should().Be("global-prompt.md");
	}

	[Fact]
	public void OverrideAllowedToolsWhenRepoSpecifiesThem()
	{
		var repoSettings = new ClaudeSettings
		{
			AllowedTools = new List<string> { "Bash", "Grep" },
		};

		var result = globalSettings.MergeWith(repoSettings);

		result.AllowedTools.Should().BeEquivalentTo(new List<string> { "Bash", "Grep" });
	}

	[Fact]
	public void KeepGlobalAllowedToolsWhenRepoListIsEmpty()
	{
		var repoSettings = new ClaudeSettings { AllowedTools = new List<string>() };

		var result = globalSettings.MergeWith(repoSettings);

		result.AllowedTools.Should().BeEquivalentTo(new List<string> { "Read", "Write" });
	}

	[Fact]
	public void KeepGlobalAllowedToolsWhenRepoListIsNull()
	{
		var repoSettings = new ClaudeSettings { AllowedTools = null };

		var result = globalSettings.MergeWith(repoSettings);

		result.AllowedTools.Should().BeEquivalentTo(new List<string> { "Read", "Write" });
	}

	[Fact]
	public void OverrideTimeoutMinutesWhenRepoSpecifiesIt()
	{
		var repoSettings = new ClaudeSettings { TimeoutMinutes = 60 };

		var result = globalSettings.MergeWith(repoSettings);

		result.TimeoutMinutes.Should().Be(60);
	}

	[Fact]
	public void KeepGlobalTimeoutMinutesWhenRepoIsZero()
	{
		var repoSettings = new ClaudeSettings { TimeoutMinutes = 0 };

		var result = globalSettings.MergeWith(repoSettings);

		result.TimeoutMinutes.Should().Be(30);
	}

	[Fact]
	public void OverrideSkipPermissionsWhenRepoSetsFalse()
	{
		var repoSettings = new ClaudeSettings { SkipPermissions = false };

		var result = globalSettings.MergeWith(repoSettings);

		result.SkipPermissions.Should().BeFalse();
	}

	[Fact]
	public void ReturnNewInstanceNotMutateOriginal()
	{
		var repoSettings = new ClaudeSettings { Model = "claude-sonnet-4-6" };

		var result = globalSettings.MergeWith(repoSettings);

		result.Should().NotBeSameAs(globalSettings);
		globalSettings.Model.Should().Be("claude-opus-4-6");
	}

	[Fact]
	public void MergeMultipleOverridesAtOnce()
	{
		var repoSettings = new ClaudeSettings
		{
			Model = "claude-sonnet-4-6",
			MaxTurns = 5,
			TimeoutMinutes = 15,
		};

		var result = globalSettings.MergeWith(repoSettings);

		result.Model.Should().Be("claude-sonnet-4-6");
		result.MaxTurns.Should().Be(5);
		result.TimeoutMinutes.Should().Be(15);
		result.OutputFormat.Should().Be("json");
		result.SystemPromptFile.Should().Be("global-prompt.md");
		result.AllowedTools.Should().BeEquivalentTo(new List<string> { "Read", "Write" });
	}
}
