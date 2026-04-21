using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public class TokenLimitHeuristic : IFixHeuristic
{
	public string Recommendation
	{
		get { return "Increase token limit or simplify the task"; }
	}

	public bool Matches(ProcessResult result)
	{
		var allText = string.Join(" ", result.OutputLines.Concat(result.ErrorLines)).ToLowerInvariant();

		return allText.Contains("token limit");
	}
}
