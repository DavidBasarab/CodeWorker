using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public class FailedToStartHeuristic : IFixHeuristic
{
	public string Recommendation
	{
		get { return "Verify the Claude CLI is installed and available on PATH"; }
	}

	public bool Matches(ProcessResult result)
	{
		return result.FailedToStart;
	}
}
