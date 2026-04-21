using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public class DefaultBlockedHeuristic : IFixHeuristic
{
	public string Recommendation
	{
		get { return "Review the blocker reported by Claude and adjust the task before re-queueing"; }
	}

	public bool Matches(ProcessResult result)
	{
		return true;
	}
}
