namespace FatCat.CodeWorker.Process;

public class ProcessResult
{
	public int ExitCode { get; set; }

	public List<string> OutputLines { get; set; } = new();

	public List<string> ErrorLines { get; set; } = new();

	public bool TimedOut { get; set; }

	public bool FailedToStart { get; set; }
}
