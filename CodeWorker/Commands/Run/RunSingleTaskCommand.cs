using System.Diagnostics;
using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IRunSingleTaskCommand : ICommand { }

public class RunSingleTaskCommand(IRunClaude runClaude, IFileSystemTools fileSystemTools, ILogger logger)
	: IRunSingleTaskCommand
{
	private string markdownFilePath;
	private ClaudeSettings claudeSettings;

	public async Task Execute(string[] args)
	{
		if (!TryResolveTaskPath(args))
		{
			return;
		}

		BuildClaudeSettings();

		logger.Information("Running single task at {MarkdownFilePath}", markdownFilePath);

		var stopwatch = Stopwatch.StartNew();
		var result = await runClaude.Run(markdownFilePath, claudeSettings, []);

		stopwatch.Stop();

		LogResult(result, stopwatch.Elapsed);
	}

	private bool TryResolveTaskPath(string[] args)
	{
		if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
		{
			logger.Error("Usage: run-task <path-to-markdown-file>");
			return false;
		}

		markdownFilePath = Path.GetFullPath(args[1]);

		if (!fileSystemTools.FileExists(markdownFilePath))
		{
			logger.Error("Task file does not exist: {MarkdownFilePath}", markdownFilePath);
			return false;
		}

		return true;
	}

	private void BuildClaudeSettings()
	{
		claudeSettings = new ClaudeSettings
		{
			Model = "claude-opus-4-7",
			MaxTurns = 20,
			SkipPermissions = true,
			OutputFormat = "stream-json",
			TimeoutMinutes = 5,
			IdleTimeoutMinutes = 2,
			TranscriptPollMilliseconds = 250,
			AllowedTools = [],
		};
	}

	private void LogResult(global::FatCat.CodeWorker.Process.ProcessResult result, TimeSpan elapsed)
	{
		logger.Information(
			"Single task complete in {Seconds}s — ExitCode={ExitCode}, TimedOut={TimedOut}, StopReason={StopReason}, OutputLines={OutputLines}",
			elapsed.TotalSeconds,
			result.ExitCode,
			result.TimedOut,
			result.TailerStopReason,
			result.OutputLines.Count
		);

		if (result.ResultEvent != null)
		{
			logger.Information("Result event: IsError={IsError}", result.ResultEvent.IsError);
		}
	}
}
