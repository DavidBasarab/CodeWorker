namespace FatCat.CodeWorker.Claude;

public class TailResult
{
	public TailerStopReason StopReason { get; set; }

	public ClaudeStreamEvent ResultEvent { get; set; }

	public ClaudeStreamEvent OrchestratorDoneEvent { get; set; }

	public List<ClaudeStreamEvent> Events { get; set; } = [];

	public int ExitCode { get; set; }

	public List<string> RawOutputLines { get; set; } = [];
}
