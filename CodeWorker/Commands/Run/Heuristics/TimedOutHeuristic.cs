using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public class TimedOutHeuristic : IFixHeuristic
{
	public string Recommendation
	{
		get { return "Increase timeout or break the task into smaller pieces"; }
	}

	public bool Matches(ProcessResult result)
	{
		return result.TimedOut;
	}
}
