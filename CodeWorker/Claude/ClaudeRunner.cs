using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Claude;

public interface IRunClaude
{
	Task<ProcessResult> Run(string markdownFilePath, ClaudeSettings claudeSettings, List<ReferenceFile> referenceFiles);
}

public class ClaudeRunner(
	IFileSystemTools fileSystemTools,
	IBuildReferenceSystemPrompt buildReferenceSystemPrompt,
	ITranscriptPaths transcriptPaths,
	IExtractWrapperScript extractWrapperScript,
	ILaunchWrapper launchWrapper,
	IClaudeTranscriptTailer tailer,
	ILogger logger
) : IRunClaude
{
	private const int DefaultIdleTimeoutMinutes = 10;
	private const int DefaultPollMilliseconds = 250;

	public async Task<ProcessResult> Run(
		string markdownFilePath,
		ClaudeSettings claudeSettings,
		List<ReferenceFile> referenceFiles
	)
	{
		LogStartup(markdownFilePath, claudeSettings);

		var paths = transcriptPaths.For(markdownFilePath);

		await PreparePromptFile(markdownFilePath, paths);
		ClearStaleArtifacts(paths);

		var claudeArgs = BuildClaudeArgs(claudeSettings, referenceFiles);
		var scriptPath = extractWrapperScript.Extract();

		var processId = launchWrapper.Launch(
			new WrapperLaunchSettings
			{
				ScriptPath = scriptPath,
				PromptFile = paths.PromptFile,
				TranscriptFile = paths.TranscriptFile,
				StderrFile = paths.StderrFile,
				DoneSentinel = paths.DoneSentinel,
				PidFile = paths.PidFile,
				ClaudeArgs = claudeArgs,
				WorkingDirectory = Path.GetDirectoryName(markdownFilePath),
			}
		);

		var tailResult = await tailer.Tail(BuildTailRequest(claudeSettings, paths, processId), new ClaudeProgressTracker());

		await CopyTranscriptToLiveLog(paths);

		return BuildProcessResult(tailResult);
	}

	private async Task PreparePromptFile(string markdownFilePath, TranscriptPaths paths)
	{
		var promptContent = await fileSystemTools.ReadAllText(markdownFilePath);

		await fileSystemTools.WriteAllText(paths.PromptFile, promptContent);
	}

	private void ClearStaleArtifacts(TranscriptPaths paths)
	{
		fileSystemTools.DeleteFile(paths.TranscriptFile);
		fileSystemTools.DeleteFile(paths.StderrFile);
		fileSystemTools.DeleteFile(paths.DoneSentinel);
		fileSystemTools.DeleteFile(paths.PidFile);
		fileSystemTools.DeleteFile(paths.LiveLogFile);
	}

	private List<string> BuildClaudeArgs(ClaudeSettings claudeSettings, List<ReferenceFile> referenceFiles)
	{
		var args = new List<string>();

		AppendModel(args, claudeSettings);
		AppendMaxTurns(args, claudeSettings);
		AppendSystemPromptFile(args, claudeSettings);
		AppendSkipPermissions(args, claudeSettings);
		AppendAllowedTools(args, claudeSettings);
		AppendReferenceFiles(args, referenceFiles);

		return args;
	}

	private void AppendModel(List<string> args, ClaudeSettings claudeSettings)
	{
		if (string.IsNullOrEmpty(claudeSettings.Model))
		{
			return;
		}

		args.Add("--model");
		args.Add(claudeSettings.Model);
	}

	private void AppendMaxTurns(List<string> args, ClaudeSettings claudeSettings)
	{
		if (claudeSettings.MaxTurns <= 0)
		{
			return;
		}

		args.Add("--max-turns");
		args.Add(claudeSettings.MaxTurns.ToString());
	}

	private void AppendSystemPromptFile(List<string> args, ClaudeSettings claudeSettings)
	{
		if (string.IsNullOrEmpty(claudeSettings.SystemPromptFile))
		{
			return;
		}

		args.Add("--system-prompt");
		args.Add(claudeSettings.SystemPromptFile);
	}

	private void AppendSkipPermissions(List<string> args, ClaudeSettings claudeSettings)
	{
		if (!claudeSettings.SkipPermissions)
		{
			return;
		}

		args.Add("--dangerously-skip-permissions");
	}

	private void AppendAllowedTools(List<string> args, ClaudeSettings claudeSettings)
	{
		if (claudeSettings.AllowedTools is not { Count: > 0 })
		{
			return;
		}

		foreach (var tool in claudeSettings.AllowedTools)
		{
			args.Add("--allowedTools");
			args.Add(tool);
		}
	}

	private void AppendReferenceFiles(List<string> args, List<ReferenceFile> referenceFiles)
	{
		if (referenceFiles is not { Count: > 0 })
		{
			return;
		}

		var content = buildReferenceSystemPrompt.Build(referenceFiles);

		args.Add("--append-system-prompt");
		args.Add(content);
	}

	private TailRequest BuildTailRequest(ClaudeSettings claudeSettings, TranscriptPaths paths, int processId)
	{
		var idleMinutes = claudeSettings.IdleTimeoutMinutes > 0 ? claudeSettings.IdleTimeoutMinutes : DefaultIdleTimeoutMinutes;
		var pollMilliseconds =
			claudeSettings.TranscriptPollMilliseconds > 0 ? claudeSettings.TranscriptPollMilliseconds : DefaultPollMilliseconds;
		var wallClock = claudeSettings.TimeoutMinutes > 0 ? TimeSpan.FromMinutes(claudeSettings.TimeoutMinutes) : TimeSpan.Zero;

		return new TailRequest
		{
			TaskName = paths.TaskName,
			TranscriptPath = paths.TranscriptFile,
			DoneSentinelPath = paths.DoneSentinel,
			LiveLogPath = paths.LiveLogFile,
			PollInterval = TimeSpan.FromMilliseconds(pollMilliseconds),
			IdleTimeout = TimeSpan.FromMinutes(idleMinutes),
			WallClockTimeout = wallClock,
			OrchestratorProcessId = processId,
		};
	}

	private async Task CopyTranscriptToLiveLog(TranscriptPaths paths)
	{
		if (!fileSystemTools.FileExists(paths.TranscriptFile))
		{
			return;
		}

		try
		{
			var content = await fileSystemTools.ReadAllText(paths.TranscriptFile);

			await fileSystemTools.WriteAllText(paths.LiveLogFile, content);
		}
		catch (Exception exception)
		{
			logger.Warning(exception, "Failed to copy transcript to live log");
		}
	}

	private ProcessResult BuildProcessResult(TailResult tailResult)
	{
		var exitCode = ResolveExitCode(tailResult);
		var timedOut =
			tailResult.StopReason == TailerStopReason.WallClockTimeout || tailResult.StopReason == TailerStopReason.IdleTimeout;

		var result = new ProcessResult
		{
			ExitCode = exitCode,
			TimedOut = timedOut,
			OutputLines = tailResult.RawOutputLines.ToList(),
			ErrorLines = [],
			ResultEvent = tailResult.ResultEvent,
			TailerStopReason = tailResult.StopReason,
		};

		LogOutcome(tailResult, result);

		return result;
	}

	private int ResolveExitCode(TailResult tailResult)
	{
		if (tailResult.OrchestratorDoneEvent?.ExitCode is int orchestratorExit)
		{
			return orchestratorExit;
		}

		if (tailResult.ExitCode != 0)
		{
			return tailResult.ExitCode;
		}

		if (tailResult.ResultEvent != null)
		{
			return tailResult.ResultEvent.IsError ? 1 : 0;
		}

		return tailResult.ExitCode;
	}

	private void LogOutcome(TailResult tailResult, ProcessResult result)
	{
		logger.Information(
			"Claude tail finished StopReason={StopReason} ExitCode={ExitCode} Events={Events}",
			tailResult.StopReason,
			result.ExitCode,
			tailResult.Events.Count
		);

		if (result.ExitCode != 0)
		{
			logger.Warning("Claude exited with non-zero exit code {ExitCode}", result.ExitCode);
		}

		logger.Information("Claude exited with code {ExitCode}", result.ExitCode);
	}

	private void LogStartup(string markdownFilePath, ClaudeSettings claudeSettings)
	{
		logger.Information("Starting Claude with markdown file {MarkdownFilePath}", markdownFilePath);

		logger.Information(
			"Claude settings: Model={Model}, MaxTurns={MaxTurns}, SkipPermissions={SkipPermissions}, OutputFormat={OutputFormat}, TimeoutMinutes={TimeoutMinutes}",
			claudeSettings.Model,
			claudeSettings.MaxTurns,
			claudeSettings.SkipPermissions,
			claudeSettings.OutputFormat,
			claudeSettings.TimeoutMinutes
		);
	}
}
