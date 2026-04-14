using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Claude;

public class ClaudeRunnerTests
{
	private readonly IRunProcess runProcess;
	private readonly ILogger logger;
	private readonly ClaudeRunner claudeRunner;
	private readonly string markdownFilePath = @"C:\Tasks\some-task.md";
	private ProcessResult processResult;
	private ProcessSettings capturedSettings;
	private ClaudeSettings claudeSettings;

	public ClaudeRunnerTests()
	{
		runProcess = A.Fake<IRunProcess>();
		logger = A.Fake<ILogger>();

		processResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "Claude output line 1", "Claude output line 2" },
		};

		A.CallTo(() => runProcess.Run(A<ProcessSettings>.Ignored))
			.ReturnsLazily(
				(ProcessSettings settings) =>
				{
					capturedSettings = settings;

					return Task.FromResult(processResult);
				}
			);

		claudeSettings = new ClaudeSettings
		{
			Model = "",
			MaxTurns = 0,
			SkipPermissions = false,
			OutputFormat = "",
			SystemPromptFile = "",
			AllowedTools = new List<string>(),
			TimeoutMinutes = 0,
		};

		claudeRunner = new ClaudeRunner(runProcess, logger);
	}

	[Fact]
	public async Task PassClaudeAsTheFileName()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.FileName.Should().Be("claude");
	}

	[Fact]
	public async Task IncludeInputFileInArguments()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().Contain($"--input-file \"{markdownFilePath}\"");
	}

	[Fact]
	public async Task IncludePrintFlagInArguments()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().Contain("-p");
	}

	[Fact]
	public async Task ReturnTheProcessResult()
	{
		var result = await claudeRunner.Run(markdownFilePath, claudeSettings);

		result.Should().BeSameAs(processResult);
	}

	[Fact]
	public async Task LogTheStartOfTheClaudeRun()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings);

		A.CallTo(() => logger.Information("Starting Claude with markdown file {MarkdownFilePath}", markdownFilePath))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogTheExitCode()
	{
		processResult.ExitCode = 42;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		A.CallTo(() => logger.Information("Claude exited with code {ExitCode}", 42)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWarningWhenExitCodeIsNotZero()
	{
		processResult.ExitCode = 1;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		A.CallTo(() => logger.Warning("Claude exited with non-zero exit code {ExitCode}", 1)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotLogWarningWhenExitCodeIsZero()
	{
		processResult.ExitCode = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		A.CallTo(() => logger.Warning(A<string>.Ignored, A<int>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task IncludeModelFlagWhenModelIsSet()
	{
		claudeSettings.Model = "claude-sonnet-4-6";

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().Contain("--model claude-sonnet-4-6");
	}

	[Fact]
	public async Task NotIncludeModelFlagWhenModelIsEmpty()
	{
		claudeSettings.Model = "";

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().NotContain("--model");
	}

	[Fact]
	public async Task NotIncludeModelFlagWhenModelIsNull()
	{
		claudeSettings.Model = null;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().NotContain("--model");
	}

	[Fact]
	public async Task IncludeMaxTurnsFlagWhenMaxTurnsIsSet()
	{
		claudeSettings.MaxTurns = 25;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().Contain("--max-turns 25");
	}

	[Fact]
	public async Task NotIncludeMaxTurnsFlagWhenMaxTurnsIsZero()
	{
		claudeSettings.MaxTurns = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().NotContain("--max-turns");
	}

	[Fact]
	public async Task IncludeOutputFormatFlagWhenOutputFormatIsSet()
	{
		claudeSettings.OutputFormat = "json";

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().Contain("--output-format json");
	}

	[Fact]
	public async Task NotIncludeOutputFormatFlagWhenOutputFormatIsEmpty()
	{
		claudeSettings.OutputFormat = "";

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().NotContain("--output-format");
	}

	[Fact]
	public async Task IncludeSystemPromptFlagWhenSystemPromptFileIsSet()
	{
		claudeSettings.SystemPromptFile = "prompt.md";

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().Contain("--system-prompt \"prompt.md\"");
	}

	[Fact]
	public async Task NotIncludeSystemPromptFlagWhenSystemPromptFileIsEmpty()
	{
		claudeSettings.SystemPromptFile = "";

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().NotContain("--system-prompt");
	}

	[Fact]
	public async Task IncludeDangerouslySkipPermissionsFlagWhenTrue()
	{
		claudeSettings.SkipPermissions = true;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().Contain("--dangerously-skip-permissions");
	}

	[Fact]
	public async Task NotIncludeDangerouslySkipPermissionsFlagWhenFalse()
	{
		claudeSettings.SkipPermissions = false;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().NotContain("--dangerously-skip-permissions");
	}

	[Fact]
	public async Task IncludeAllowedToolsFlagsWhenToolsAreSet()
	{
		claudeSettings.AllowedTools = new List<string> { "Read", "Write" };

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().Contain("--allowedTools \"Read\"");
		capturedSettings.Arguments.Should().Contain("--allowedTools \"Write\"");
	}

	[Fact]
	public async Task NotIncludeAllowedToolsFlagWhenToolsListIsEmpty()
	{
		claudeSettings.AllowedTools = new List<string>();

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().NotContain("--allowedTools");
	}

	[Fact]
	public async Task NotIncludeAllowedToolsFlagWhenToolsListIsNull()
	{
		claudeSettings.AllowedTools = null;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.Arguments.Should().NotContain("--allowedTools");
	}

	[Fact]
	public async Task SetTimeoutMillisecondsFromTimeoutMinutes()
	{
		claudeSettings.TimeoutMinutes = 30;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.TimeoutMilliseconds.Should().Be(30 * 60 * 1000);
	}

	[Fact]
	public async Task NotSetTimeoutWhenTimeoutMinutesIsZero()
	{
		claudeSettings.TimeoutMinutes = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		capturedSettings.TimeoutMilliseconds.Should().Be(0);
	}

	[Fact]
	public async Task LogEffectiveSettings()
	{
		claudeSettings.Model = "claude-sonnet-4-6";
		claudeSettings.MaxTurns = 15;

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		A.CallTo(() =>
				logger.Information(
					"Claude settings: Model={Model}, MaxTurns={MaxTurns}, SkipPermissions={SkipPermissions}, OutputFormat={OutputFormat}, TimeoutMinutes={TimeoutMinutes}",
					A<object[]>.Ignored
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task BuildCompleteArgumentsWithAllSettings()
	{
		claudeSettings = new ClaudeSettings
		{
			Model = "claude-opus-4-6",
			MaxTurns = 10,
			SkipPermissions = true,
			OutputFormat = "json",
			SystemPromptFile = "system.md",
			AllowedTools = new List<string> { "Read" },
			TimeoutMinutes = 30,
		};

		await claudeRunner.Run(markdownFilePath, claudeSettings);

		var args = capturedSettings.Arguments;

		args.Should().Contain("-p");
		args.Should().Contain($"--input-file \"{markdownFilePath}\"");
		args.Should().Contain("--model claude-opus-4-6");
		args.Should().Contain("--max-turns 10");
		args.Should().Contain("--output-format json");
		args.Should().Contain("--system-prompt \"system.md\"");
		args.Should().Contain("--dangerously-skip-permissions");
		args.Should().Contain("--allowedTools \"Read\"");
	}
}
