namespace FatCat.CodeWorker.Claude;

public class TailRequest
{
	public string TranscriptPath { get; set; }

	public string DoneSentinelPath { get; set; }

	public string LiveLogPath { get; set; }

	public TimeSpan PollInterval { get; set; }

	public TimeSpan IdleTimeout { get; set; }

	public TimeSpan WallClockTimeout { get; set; }

	public int OrchestratorProcessId { get; set; }

	public string TaskName { get; set; }
}
