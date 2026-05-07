using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Claude;

public interface IRecoverPendingTasks
{
	Task Recover(RepositorySettings repository, RepoSettings repoSettings, TaskFolders folders);
}

public class RecoverPendingTasks(
	IFileSystemTools fileSystemTools,
	ITranscriptPaths transcriptPaths,
	IClassifyTaskResult classifyTaskResult,
	IMoveTask moveTask,
	ILogger logger
) : IRecoverPendingTasks
{
	public async Task Recover(RepositorySettings repository, RepoSettings repoSettings, TaskFolders folders)
	{
		if (!fileSystemTools.DirectoryExists(folders.Pending))
		{
			return;
		}

		var pendingFiles = fileSystemTools.GetFiles(folders.Pending);

		foreach (var pendingFile in pendingFiles.Where(IsTaskMarkdown))
		{
			await TryRecover(pendingFile, folders);
		}
	}

	private async Task TryRecover(string pendingTaskFile, TaskFolders folders)
	{
		var paths = transcriptPaths.For(pendingTaskFile);

		if (!fileSystemTools.FileExists(paths.DoneSentinel))
		{
			logger.Information("Pending task {TaskName} has no done sentinel — leaving for stalled handling", paths.TaskName);

			return;
		}

		logger.Information(
			"Recovering pending task {TaskName} — done sentinel present at {DoneSentinel}",
			paths.TaskName,
			paths.DoneSentinel
		);

		var processResult = await BuildProcessResultFromTranscript(paths);
		var outcome = classifyTaskResult.Classify(processResult);
		var destination = ResolveDestination(outcome, folders);

		logger.Information(
			"Recovered task {TaskName} classified as {Outcome}; moving to {Destination}",
			paths.TaskName,
			outcome,
			destination
		);

		moveTask.Move(pendingTaskFile, destination);

		ArchiveTranscript(paths, folders);
	}

	private async Task<ProcessResult> BuildProcessResultFromTranscript(TranscriptPaths paths)
	{
		var result = new ProcessResult();

		if (!fileSystemTools.FileExists(paths.TranscriptFile))
		{
			result.ExitCode = ReadSentinelExitCode(paths.DoneSentinel);

			return result;
		}

		var lines = await fileSystemTools.ReadAllLines(paths.TranscriptFile);

		foreach (var line in lines)
		{
			result.OutputLines.Add(line);

			if (!ClaudeStreamEvent.TryParse(line, out var streamEvent))
			{
				continue;
			}

			if (streamEvent.Kind == ClaudeEventKind.Result)
			{
				result.ResultEvent = streamEvent;
			}

			if (streamEvent.Kind == ClaudeEventKind.OrchestratorDone && streamEvent.ExitCode.HasValue)
			{
				result.ExitCode = streamEvent.ExitCode.Value;
			}
		}

		if (result.ExitCode == 0 && result.ResultEvent == null)
		{
			result.ExitCode = ReadSentinelExitCode(paths.DoneSentinel);
		}

		return result;
	}

	private int ReadSentinelExitCode(string sentinelPath)
	{
		try
		{
			var content = File.ReadAllText(sentinelPath).Trim();

			return int.TryParse(content, out var exitCode) ? exitCode : -1;
		}
		catch (Exception exception)
		{
			logger.Warning(exception, "Failed to read done sentinel {SentinelPath}", sentinelPath);

			return -1;
		}
	}

	private void ArchiveTranscript(TranscriptPaths paths, TaskFolders folders)
	{
		fileSystemTools.EnsureDirectory(folders.Logs);

		MoveIfExists(paths.TranscriptFile, folders.Logs);
		MoveIfExists(paths.StderrFile, folders.Logs);
		MoveIfExists(paths.LiveLogFile, folders.Logs);
		fileSystemTools.DeleteFile(paths.DoneSentinel);
		fileSystemTools.DeleteFile(paths.PidFile);
		fileSystemTools.DeleteFile(paths.PromptFile);
	}

	private void MoveIfExists(string source, string destinationFolder)
	{
		if (!fileSystemTools.FileExists(source))
		{
			return;
		}

		var destination = Path.Combine(destinationFolder, Path.GetFileName(source));

		fileSystemTools.MoveFile(source, destination);
	}

	private string ResolveDestination(TaskOutcome outcome, TaskFolders folders)
	{
		return outcome switch
		{
			TaskOutcome.Done => folders.Done,
			TaskOutcome.Blocked => folders.Blocked,
			TaskOutcome.Failed => folders.Failed,
			_ => throw new ArgumentOutOfRangeException(nameof(outcome)),
		};
	}

	private static bool IsTaskMarkdown(string path)
	{
		var name = Path.GetFileName(path);

		if (string.IsNullOrEmpty(name))
		{
			return false;
		}

		return name.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
	}
}
