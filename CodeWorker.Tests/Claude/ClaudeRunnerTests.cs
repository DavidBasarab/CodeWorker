using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Claude;

public class ClaudeRunnerTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly IBuildReferenceSystemPrompt buildReferenceSystemPrompt;
	private readonly ITranscriptPaths transcriptPaths;
	private readonly IExtractWrapperScript extractWrapperScript;
	private readonly ILaunchWrapper launchWrapper;
	private readonly IClaudeTranscriptTailer tailer;
	private readonly ILogger logger;
	private readonly ClaudeRunner claudeRunner;

	private readonly string markdownFilePath = @"C:\Tasks\some-task.md";
	private readonly string markdownFileContent = "# Task\nDo something useful";
	private readonly TranscriptPaths paths;
	private readonly List<ReferenceFile> referenceFiles;
	private ClaudeSettings claudeSettings;
	private TailResult tailResult;
	private WrapperLaunchSettings capturedLaunchSettings;
	private TailRequest capturedTailRequest;

	public ClaudeRunnerTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		buildReferenceSystemPrompt = A.Fake<IBuildReferenceSystemPrompt>();
		transcriptPaths = A.Fake<ITranscriptPaths>();
		extractWrapperScript = A.Fake<IExtractWrapperScript>();
		launchWrapper = A.Fake<ILaunchWrapper>();
		tailer = A.Fake<IClaudeTranscriptTailer>();
		logger = A.Fake<ILogger>();

		paths = new TranscriptPaths
		{
			TaskName = "some-task.md",
			PromptFile = @"C:\Tasks\some-task.prompt.txt",
			TranscriptFile = @"C:\Tasks\some-task.transcript.jsonl",
			StderrFile = @"C:\Tasks\some-task.stderr.log",
			DoneSentinel = @"C:\Tasks\some-task.done",
			PidFile = @"C:\Tasks\some-task.wrapper.pid",
			LiveLogFile = @"C:\Tasks\some-task.live.log",
		};

		referenceFiles = new List<ReferenceFile>();

		claudeSettings = new ClaudeSettings
		{
			Model = "",
			MaxTurns = 0,
			SkipPermissions = false,
			OutputFormat = "stream-json",
			SystemPromptFile = "",
			AllowedTools = new List<string>(),
			TimeoutMinutes = 0,
		};

		tailResult = new TailResult { StopReason = TailerStopReason.OrchestratorDone, ExitCode = 0 };

		A.CallTo(() => fileSystemTools.ReadAllText(markdownFilePath)).Returns(Task.FromResult(markdownFileContent));
		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).Returns(false);
		A.CallTo(() => transcriptPaths.For(markdownFilePath)).Returns(paths);
		A.CallTo(() => extractWrapperScript.Extract()).Returns(@"C:\Temp\Run-ClaudeTask.ps1");

		A.CallTo(() => launchWrapper.Launch(A<WrapperLaunchSettings>._))
			.ReturnsLazily(
				(WrapperLaunchSettings settings) =>
				{
					capturedLaunchSettings = settings;

					return 12345;
				}
			);

		A.CallTo(() => tailer.Tail(A<TailRequest>._, A<ClaudeProgressTracker>._))
			.ReturnsLazily(
				(TailRequest request, ClaudeProgressTracker tracker) =>
				{
					capturedTailRequest = request;

					return Task.FromResult(tailResult);
				}
			);

		A.CallTo(() => buildReferenceSystemPrompt.Build(A<List<ReferenceFile>>._)).Returns("ref-content");

		claudeRunner = new ClaudeRunner(
			fileSystemTools,
			buildReferenceSystemPrompt,
			transcriptPaths,
			extractWrapperScript,
			launchWrapper,
			tailer,
			logger
		);
	}

	[Fact]
	public async Task ReadTheMarkdownFile()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => fileSystemTools.ReadAllText(markdownFilePath)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteThePromptFileForTheWrapper()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => fileSystemTools.WriteAllText(paths.PromptFile, markdownFileContent)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ClearStaleTranscriptBeforeRunning()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => fileSystemTools.DeleteFile(paths.TranscriptFile)).MustHaveHappened();
	}

	[Fact]
	public async Task ClearStaleStderrBeforeRunning()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => fileSystemTools.DeleteFile(paths.StderrFile)).MustHaveHappened();
	}

	[Fact]
	public async Task ClearStaleDoneSentinelBeforeRunning()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => fileSystemTools.DeleteFile(paths.DoneSentinel)).MustHaveHappened();
	}

	[Fact]
	public async Task ExtractTheWrapperScript()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => extractWrapperScript.Extract()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LaunchTheWrapperWithTheExtractedScript()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ScriptPath.Should().Be(@"C:\Temp\Run-ClaudeTask.ps1");
	}

	[Fact]
	public async Task LaunchTheWrapperWithTheCorrectPromptFile()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.PromptFile.Should().Be(paths.PromptFile);
	}

	[Fact]
	public async Task LaunchTheWrapperWithTheCorrectTranscriptFile()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.TranscriptFile.Should().Be(paths.TranscriptFile);
	}

	[Fact]
	public async Task LaunchTheWrapperWithTheCorrectDoneSentinel()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.DoneSentinel.Should().Be(paths.DoneSentinel);
	}

	[Fact]
	public async Task IncludeModelInClaudeArgsWhenSet()
	{
		claudeSettings.Model = "claude-opus-4-6";

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().Contain("--model");
		capturedLaunchSettings.ClaudeArgs.Should().Contain("claude-opus-4-6");
	}

	[Fact]
	public async Task NotIncludeModelArgWhenEmpty()
	{
		claudeSettings.Model = "";

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().NotContain("--model");
	}

	[Fact]
	public async Task IncludeMaxTurnsInClaudeArgsWhenSet()
	{
		claudeSettings.MaxTurns = 25;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().Contain("--max-turns");
		capturedLaunchSettings.ClaudeArgs.Should().Contain("25");
	}

	[Fact]
	public async Task NotIncludeMaxTurnsArgWhenZero()
	{
		claudeSettings.MaxTurns = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().NotContain("--max-turns");
	}

	[Fact]
	public async Task IncludeSkipPermissionsArgWhenTrue()
	{
		claudeSettings.SkipPermissions = true;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().Contain("--dangerously-skip-permissions");
	}

	[Fact]
	public async Task NotIncludeSkipPermissionsArgWhenFalse()
	{
		claudeSettings.SkipPermissions = false;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().NotContain("--dangerously-skip-permissions");
	}

	[Fact]
	public async Task IncludeAllowedToolsArgsWhenSet()
	{
		claudeSettings.AllowedTools = new List<string> { "Read", "Write" };

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().Contain("Read");
		capturedLaunchSettings.ClaudeArgs.Should().Contain("Write");
	}

	[Fact]
	public async Task IncludeAppendSystemPromptWhenReferenceFilesExist()
	{
		referenceFiles.Add(new ReferenceFile { Name = "context.md", Content = "context content" });

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().Contain("--append-system-prompt");
	}

	[Fact]
	public async Task NotIncludeAppendSystemPromptWhenReferenceFilesEmpty()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedLaunchSettings.ClaudeArgs.Should().NotContain("--append-system-prompt");
	}

	[Fact]
	public async Task PassWallClockTimeoutFromTimeoutMinutes()
	{
		claudeSettings.TimeoutMinutes = 30;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedTailRequest.WallClockTimeout.Should().Be(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public async Task DefaultIdleTimeoutWhenSettingIsZero()
	{
		claudeSettings.IdleTimeoutMinutes = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedTailRequest.IdleTimeout.Should().Be(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public async Task UseConfiguredIdleTimeoutWhenSet()
	{
		claudeSettings.IdleTimeoutMinutes = 5;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedTailRequest.IdleTimeout.Should().Be(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public async Task DefaultPollIntervalWhenSettingIsZero()
	{
		claudeSettings.TranscriptPollMilliseconds = 0;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedTailRequest.PollInterval.Should().Be(TimeSpan.FromMilliseconds(250));
	}

	[Fact]
	public async Task UseConfiguredPollIntervalWhenSet()
	{
		claudeSettings.TranscriptPollMilliseconds = 1000;

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		capturedTailRequest.PollInterval.Should().Be(TimeSpan.FromMilliseconds(1000));
	}

	[Fact]
	public async Task ReturnExitCodeFromTailResult()
	{
		tailResult.ExitCode = 42;
		tailResult.OrchestratorDoneEvent = new ClaudeStreamEvent { ExitCode = 42, Kind = ClaudeEventKind.OrchestratorDone };

		var result = await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		result.ExitCode.Should().Be(42);
	}

	[Fact]
	public async Task SetTimedOutWhenTailerHitsWallClockTimeout()
	{
		tailResult.StopReason = TailerStopReason.WallClockTimeout;
		tailResult.ExitCode = -1;

		var result = await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		result.TimedOut.Should().BeTrue();
	}

	[Fact]
	public async Task SetTimedOutWhenTailerHitsIdleTimeout()
	{
		tailResult.StopReason = TailerStopReason.IdleTimeout;
		tailResult.ExitCode = -1;

		var result = await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		result.TimedOut.Should().BeTrue();
	}

	[Fact]
	public async Task NotSetTimedOutWhenOrchestratorDone()
	{
		tailResult.StopReason = TailerStopReason.OrchestratorDone;

		var result = await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		result.TimedOut.Should().BeFalse();
	}

	[Fact]
	public async Task PassResultEventThroughOnProcessResult()
	{
		var resultEvent = new ClaudeStreamEvent { Kind = ClaudeEventKind.Result, Subtype = "success" };
		tailResult.ResultEvent = resultEvent;

		var result = await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		result.ResultEvent.Should().BeSameAs(resultEvent);
	}

	[Fact]
	public async Task PassTailerStopReasonThroughOnProcessResult()
	{
		tailResult.StopReason = TailerStopReason.IdleTimeout;

		var result = await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		result.TailerStopReason.Should().Be(TailerStopReason.IdleTimeout);
	}

	[Fact]
	public async Task LogTheStartOfTheClaudeRun()
	{
		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => logger.Information("Starting Claude with markdown file {MarkdownFilePath}", markdownFilePath))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWarningWhenExitCodeIsNotZero()
	{
		tailResult.ExitCode = 1;
		tailResult.OrchestratorDoneEvent = new ClaudeStreamEvent { ExitCode = 1, Kind = ClaudeEventKind.OrchestratorDone };

		await claudeRunner.Run(markdownFilePath, claudeSettings, referenceFiles);

		A.CallTo(() => logger.Warning("Claude exited with non-zero exit code {ExitCode}", 1)).MustHaveHappenedOnceExactly();
	}
}
