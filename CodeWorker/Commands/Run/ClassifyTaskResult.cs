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
		if (result.TimedOut || result.FailedToStart)
		{
			return TaskOutcome.Failed;
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

	private static bool HasBlockedMarker(ProcessResult result)
	{
		return result.OutputLines.Any(StartsWithMarker) || result.ErrorLines.Any(StartsWithMarker);
	}

	private static bool StartsWithMarker(string line)
	{
		return line.TrimStart().StartsWith(BlockedMarker, StringComparison.OrdinalIgnoreCase);
	}
}
