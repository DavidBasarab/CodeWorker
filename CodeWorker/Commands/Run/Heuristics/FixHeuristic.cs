using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run.Heuristics;

public interface IFixHeuristic
{
	bool Matches(ProcessResult result);

	string Recommendation { get; }
}
