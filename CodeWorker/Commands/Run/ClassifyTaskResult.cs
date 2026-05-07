using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run;

public interface IClassifyTaskResult
{
	TaskOutcome Classify(ProcessResult result);
}

public class ClassifyTaskResult : IClassifyTaskResult
{
	private const string BlockedMarker = "BLOCKED:";

	public TaskOutcome Classify(ProcessResult result)
	{
		if (result.FailedToStart)
		{
			return TaskOutcome.Failed;
		}

		if (result.TimedOut)
		{
			return TaskOutcome.Failed;
		}

		if (result.ResultEvent != null)
		{
			return ClassifyFromResultEvent(result.ResultEvent);
		}

		if (result.ExitCode == 0)
		{
			return TaskOutcome.Done;
		}

		if (HasBlockedMarker(result))
		{
			return TaskOutcome.Blocked;
		}

		return TaskOutcome.Failed;
	}

	private TaskOutcome ClassifyFromResultEvent(ClaudeStreamEvent resultEvent)
	{
		if (IsBlockedSubtype(resultEvent.Subtype))
		{
			return TaskOutcome.Blocked;
		}

		if (IsBlockedResultText(resultEvent.ResultText))
		{
			return TaskOutcome.Blocked;
		}

		if (resultEvent.IsError)
		{
			return TaskOutcome.Failed;
		}

		if (resultEvent.Subtype == "success")
		{
			return TaskOutcome.Done;
		}

		return TaskOutcome.Failed;
	}

	private static bool IsBlockedSubtype(string subtype)
	{
		if (string.IsNullOrEmpty(subtype))
		{
			return false;
		}

		return subtype.Equals("error_max_turns", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsBlockedResultText(string resultText)
	{
		if (string.IsNullOrEmpty(resultText))
		{
			return false;
		}

		return resultText.TrimStart().StartsWith(BlockedMarker, StringComparison.OrdinalIgnoreCase);
	}

	private static bool HasBlockedMarker(ProcessResult result)
	{
		return result.OutputLines.Any(StartsWithMarker) || result.ErrorLines.Any(StartsWithMarker);
	}

	private static bool StartsWithMarker(string line)
	{
		return line.TrimStart().StartsWith(BlockedMarker, StringComparison.OrdinalIgnoreCase);
	}
}
