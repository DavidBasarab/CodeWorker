using FatCat.CodeWorker.Commands.Run;

namespace FatCat.CodeWorker.History;

public class RepositoryRunHistoryEntry
{
	public DateTime Timestamp { get; set; }

	public string TaskName { get; set; }

	public TaskOutcome Outcome { get; set; }

	public int ExitCode { get; set; }

	public long DurationMs { get; set; }

	public string CommitHash { get; set; }
}
