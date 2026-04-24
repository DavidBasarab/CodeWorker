using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public class DefaultFailedHeuristic : IFixHeuristic
{
	public string Recommendation
	{
		get { return "Review the error output above and address the root cause"; }
	}

	public bool Matches(ProcessResult result)
	{
		return true;
	}
}
