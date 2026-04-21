using FatCat.CodeWorker.Process;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IGenerateFailedExplanation
{
	Task Generate(string failedFolder, string taskName, ProcessResult result);
}

public class GenerateFailedExplanation(IFileSystemTools fileSystemTools, ILogger logger) : IGenerateFailedExplanation
{
	public async Task Generate(string failedFolder, string taskName, ProcessResult result)
	{
		var baseName = Path.GetFileNameWithoutExtension(taskName);
		var explanationFileName = $"{baseName}.failed.md";
		var explanationFilePath = Path.Combine(failedFolder, explanationFileName);

		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		var failureMode = DetermineFailureMode(result);
		var recommendedFix = DetermineRecommendedFix(result);
		var claudeOutput = string.Join(Environment.NewLine, result.OutputLines);
		var errorOutput = string.Join(Environment.NewLine, result.ErrorLines);

		var content =
			$"# Failed Task: {taskName}\n\n"
			+ $"**Timestamp:** {timestamp}\n\n"
			+ $"## Failure Mode\n\n"
			+ $"{failureMode}\n\n"
			+ $"## Exit Code\n\n"
			+ $"Exit Code: {result.ExitCode}\n\n"
			+ $"## Claude Output\n\n"
			+ $"{claudeOutput}\n\n"
			+ $"## Error Output\n\n"
			+ $"{errorOutput}\n\n"
			+ $"## Recommended Fix\n\n"
			+ $"{recommendedFix}\n";

		logger.Information("Generating failed explanation for task {TaskName} at {Path}", taskName, explanationFilePath);

		await fileSystemTools.WriteAllText(explanationFilePath, content);
	}

	private static string DetermineFailureMode(ProcessResult result)
	{
		if (result.TimedOut)
		{
			return "Timed Out";
		}

		if (result.FailedToStart)
		{
			return "Failed To Start";
		}

		return "Execution Error";
	}

	private static string DetermineRecommendedFix(ProcessResult result)
	{
		if (result.TimedOut)
		{
			return "Increase timeout or break the task into smaller pieces";
		}

		if (result.FailedToStart)
		{
			return "Verify the Claude CLI is installed and available on PATH";
		}

		var allText = string.Join(" ", result.OutputLines.Concat(result.ErrorLines)).ToLowerInvariant();

		if (allText.Contains("token limit"))
		{
			return "Increase token limit or simplify the task";
		}

		if (allText.Contains("authentication") || allText.Contains("unauthorized"))
		{
			return "Check API key and authentication configuration";
		}

		return "Review the error output above and address the root cause";
	}
}
