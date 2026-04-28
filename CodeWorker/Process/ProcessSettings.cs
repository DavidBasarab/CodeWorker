namespace FatCat.CodeWorker.Process;

public class ProcessSettings
{
	public string FileName { get; set; }

	public string Arguments { get; set; }

	public string WorkingDirectory { get; set; }

	public int TimeoutMilliseconds { get; set; }

	public string StandardInput { get; set; }

	public string LiveLogPath { get; set; }
}
