using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public class MissingResourceHeuristic : IFixHeuristic
{
	public string Recommendation
	{
		get { return "Resolve the missing resource referenced by the task, then re-queue"; }
	}

	public bool Matches(ProcessResult result)
	{
		var allText = string.Join(" ", result.OutputLines.Concat(result.ErrorLines)).ToLowerInvariant();

		return allText.Contains("missing") || allText.Contains("not found") || allText.Contains("does not exist");
	}
}
