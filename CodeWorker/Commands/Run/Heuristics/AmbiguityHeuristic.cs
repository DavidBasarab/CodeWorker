using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public class AmbiguityHeuristic : IFixHeuristic
{
	public string Recommendation
	{
		get { return "Clarify the task instructions to remove ambiguity, then re-queue"; }
	}

	public bool Matches(ProcessResult result)
	{
		var allText = string.Join(" ", result.OutputLines.Concat(result.ErrorLines)).ToLowerInvariant();

		return allText.Contains("contradictory") || allText.Contains("conflict") || allText.Contains("ambiguous");
	}
}
