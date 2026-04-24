using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Claude;

public class ClaudeRunnerTests
{
	private readonly IRunProcess runProcess;
	private readonly IFileSystemTools fileSystemTools;
	private readonly IBuildReferenceSystemPrompt buildReferenceSystemPrompt;
	private readonly ILogger logger;
	private readonly ClaudeRunner claudeRunner;
	private readonly string markdownFilePath = @"C:\Tasks\some-task.md";
	private readonly string markdownFileContent = "# Task\nDo something useful";
	private readonly List<ReferenceFile> referenceFiles;
	private ProcessResult processResult;
	private ProcessSettings capturedSettings;
	private ClaudeSettings claudeSettings;

	public ClaudeRunnerTests()
	{
		runProcess = A.Fake<IRunProcess>();
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		var realBuilder = new BuildReferenceSystemPrompt();
		buildReferenceSystemPrompt = A.Fake<IBuildReferenceSystemPrompt>();

		A.CallTo(() => buildReferenceSystemPrompt.Build(A<List<ReferenceFile>>._))
			.ReturnsLazily((List<ReferenceFile> files) => realBuilder.Build(files));

		A.CallTo(() => fileSystemTools.ReadAllText(markdownFilePath)).Returns(Task.FromResult(markdownFileContent));

		processResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "Claude output line 1", "Claude output line 2" },
		};

		A.CallTo(() => runProcess.Run(A<ProcessSettings>._))
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

		referenceFiles = new List<ReferenceFile>();

		claudeRunner = new ClaudeRunner(runProcess, fileSystemTools, buildReferenceSystemPrompt, logger);
	}

	[Fact]
	public async Task PassClaudeAsTheFileName()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.FileName.Should().Be("claude");
	}

	[Fact]
	public async Task ReadTheMarkdownFile()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => fileSystemTools.ReadAllText(markdownFilePath)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassFileContentAsStandardInput()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.StandardInput.Should().Be(markdownFileContent);
	}

	[Fact]
	public async Task IncludePrintFlagInArguments()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("-p");
	}

	[Fact]
	public async Task ReturnTheProcessResult()
	{
		var result = await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		result.Should().BeSameAs(processResult);
	}

	[Fact]
	public async Task LogTheStartOfTheClaudeRun()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => logger.Information("Starting Claude with markdown file {MarkdownFilePath}", markdownFilePath))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogTheExitCode()
	{
		processResult.ExitCode = 42;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => logger.Information("Claude exited with code {ExitCode}", 42)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWarningWhenExitCodeIsNotZero()
	{
		processResult.ExitCode = 1;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => logger.Warning("Claude exited with non-zero exit code {ExitCode}", 1)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotLogWarningWhenExitCodeIsZero()
	{
		processResult.ExitCode = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => logger.Warning(A<string>._, A<int>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task IncludeModelFlagWhenModelIsSet()
	{
		claudeSettings.Model = "claude-sonnet-4-6";

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("--model claude-sonnet-4-6");
	}

	[Fact]
	public async Task NotIncludeModelFlagWhenModelIsEmpty()
	{
		claudeSettings.Model = "";

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--model");
	}

	[Fact]
	public async Task NotIncludeModelFlagWhenModelIsNull()
	{
		claudeSettings.Model = null;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--model");
	}

	[Fact]
	public async Task IncludeMaxTurnsFlagWhenMaxTurnsIsSet()
	{
		claudeSettings.MaxTurns = 25;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("--max-turns 25");
	}

	[Fact]
	public async Task NotIncludeMaxTurnsFlagWhenMaxTurnsIsZero()
	{
		claudeSettings.MaxTurns = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--max-turns");
	}

	[Fact]
	public async Task IncludeOutputFormatFlagWhenOutputFormatIsSet()
	{
		claudeSettings.OutputFormat = "json";

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("--output-format json");
	}

	[Fact]
	public async Task NotIncludeOutputFormatFlagWhenOutputFormatIsEmpty()
	{
		claudeSettings.OutputFormat = "";

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--output-format");
	}

	[Fact]
	public async Task IncludeSystemPromptFlagWhenSystemPromptFileIsSet()
	{
		claudeSettings.SystemPromptFile = "prompt.md";

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("--system-prompt \"prompt.md\"");
	}

	[Fact]
	public async Task NotIncludeSystemPromptFlagWhenSystemPromptFileIsEmpty()
	{
		claudeSettings.SystemPromptFile = "";

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--system-prompt");
	}

	[Fact]
	public async Task IncludeDangerouslySkipPermissionsFlagWhenTrue()
	{
		claudeSettings.SkipPermissions = true;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("--dangerously-skip-permissions");
	}

	[Fact]
	public async Task NotIncludeDangerouslySkipPermissionsFlagWhenFalse()
	{
		claudeSettings.SkipPermissions = false;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--dangerously-skip-permissions");
	}

	[Fact]
	public async Task IncludeAllowedToolsFlagsWhenToolsAreSet()
	{
		claudeSettings.AllowedTools = new List<string> { "Read", "Write" };

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("--allowedTools \"Read\"");
		capturedSettings.Arguments.Should().Contain("--allowedTools \"Write\"");
	}

	[Fact]
	public async Task NotIncludeAllowedToolsFlagWhenToolsListIsEmpty()
	{
		claudeSettings.AllowedTools = new List<string>();

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--allowedTools");
	}

	[Fact]
	public async Task NotIncludeAllowedToolsFlagWhenToolsListIsNull()
	{
		claudeSettings.AllowedTools = null;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--allowedTools");
	}

	[Fact]
	public async Task SetTimeoutMillisecondsFromTimeoutMinutes()
	{
		claudeSettings.TimeoutMinutes = 30;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.TimeoutMilliseconds.Should().Be(30 * 60 * 1000);
	}

	[Fact]
	public async Task NotSetTimeoutWhenTimeoutMinutesIsZero()
	{
		claudeSettings.TimeoutMinutes = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.TimeoutMilliseconds.Should().Be(0);
	}

	[Fact]
	public async Task LogEffectiveSettings()
	{
		claudeSettings.Model = "claude-sonnet-4-6";
		claudeSettings.MaxTurns = 15;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() =>
				logger.Information(
					"Claude settings: Model={Model}, MaxTurns={MaxTurns}, SkipPermissions={SkipPermissions}, OutputFormat={OutputFormat}, TimeoutMinutes={TimeoutMinutes}",
					A<object[]>._
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

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		var args = capturedSettings.Arguments;

		args.Should().Contain("-p");
		args.Should().NotContain("--input-file");
		args.Should().Contain("--model claude-opus-4-6");
		args.Should().Contain("--max-turns 10");
		args.Should().Contain("--output-format json");
		args.Should().Contain("--system-prompt \"system.md\"");
		args.Should().Contain("--dangerously-skip-permissions");
		args.Should().Contain("--allowedTools \"Read\"");
	}

	[Fact]
	public async Task IncludeAppendSystemPromptWhenReferenceFilesExist()
	{
		referenceFiles.Add(new ReferenceFile { Name = "context.md", Content = "some context" });

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("--append-system-prompt");
	}

	[Fact]
	public async Task NotIncludeAppendSystemPromptWhenReferenceFilesAreEmpty()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().NotContain("--append-system-prompt");
	}

	[Fact]
	public async Task IncludeReferenceFileNameInAppendSystemPrompt()
	{
		referenceFiles.Add(new ReferenceFile { Name = "context.md", Content = "some context" });

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("context.md");
	}

	[Fact]
	public async Task IncludeReferenceFileContentInAppendSystemPrompt()
	{
		referenceFiles.Add(new ReferenceFile { Name = "context.md", Content = "important context here" });

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.Arguments.Should().Contain("important context here");
	}

	[Fact]
	public async Task IncludeMultipleReferenceFilesInAppendSystemPrompt()
	{
		referenceFiles.Add(new ReferenceFile { Name = "context.md", Content = "context content" });
		referenceFiles.Add(new ReferenceFile { Name = "schema.md", Content = "schema content" });

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		var args = capturedSettings.Arguments;

		args.Should().Contain("context.md");
		args.Should().Contain("context content");
		args.Should().Contain("schema.md");
		args.Should().Contain("schema content");
	}

	[Fact]
	public async Task PassLiveLogPathDerivedFromMarkdownFile()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedSettings.LiveLogPath.Should().Be(@"C:\Tasks\some-task.live.log");
	}
}
