using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public class AuthenticationHeuristic : IFixHeuristic
{
	public string Recommendation
	{
		get { return "Check API key and authentication configuration"; }
	}

	public bool Matches(ProcessResult result)
	{
		var allText = string.Join(" ", result.OutputLines.Concat(result.ErrorLines)).ToLowerInvariant();

		return allText.Contains("authentication") || allText.Contains("unauthorized");
	}
}
