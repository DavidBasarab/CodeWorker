using FatCat.CodeWorker.Process;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IGenerateBlockedExplanation
{
	Task Generate(string blockedFolder, string taskName, ProcessResult result);
}

public class GenerateBlockedExplanation(IFileSystemTools fileSystemTools, ILogger logger) : IGenerateBlockedExplanation
{
	public async Task Generate(string blockedFolder, string taskName, ProcessResult result)
	{
		var baseName = Path.GetFileNameWithoutExtension(taskName);
		var explanationFileName = $"{baseName}.blocked.md";
		var explanationFilePath = Path.Combine(blockedFolder, explanationFileName);

		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		var recommendedFix = DetermineRecommendedFix(result);
		var claudeOutput = string.Join(Environment.NewLine, result.OutputLines);
		var errorOutput = string.Join(Environment.NewLine, result.ErrorLines);

		var content =
			$"# Blocked Task: {taskName}\n\n"
			+ $"**Timestamp:** {timestamp}\n\n"
			+ $"## Exit Code\n\n"
			+ $"Exit Code: {result.ExitCode}\n\n"
			+ $"## Claude Output\n\n"
			+ $"{claudeOutput}\n\n"
			+ $"## Error Output\n\n"
			+ $"{errorOutput}\n\n"
			+ $"## Recommended Fix\n\n"
			+ $"{recommendedFix}\n";

		logger.Information("Generating blocked explanation for task {TaskName} at {Path}", taskName, explanationFilePath);

		await fileSystemTools.WriteAllText(explanationFilePath, content);
	}

	private static string DetermineRecommendedFix(ProcessResult result)
	{
		var allText = string.Join(" ", result.OutputLines.Concat(result.ErrorLines)).ToLowerInvariant();

		if (allText.Contains("missing") || allText.Contains("not found") || allText.Contains("does not exist"))
		{
			return "Resolve the missing resource referenced by the task, then re-queue";
		}

		if (allText.Contains("contradictory") || allText.Contains("conflict") || allText.Contains("ambiguous"))
		{
			return "Clarify the task instructions to remove ambiguity, then re-queue";
		}

		return "Review the blocker reported by Claude and adjust the task before re-queueing";
	}
}
