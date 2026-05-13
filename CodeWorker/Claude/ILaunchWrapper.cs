namespace FatCat.CodeWorker.Claude;

public interface ILaunchWrapper
{
	int Launch(WrapperLaunchSettings settings);
}

public class WrapperLaunchSettings
{
	public string ScriptPath { get; set; }

	public string PromptFile { get; set; }

	public string TranscriptFile { get; set; }

	public string StderrFile { get; set; }

	public string DoneSentinel { get; set; }

	public string PidFile { get; set; }

	public string WrapperStartedFile { get; set; }

	public string WrapperLogFile { get; set; }

	public string ClaudeArgsFile { get; set; }

	public List<string> ClaudeArgs { get; set; } = [];

	public string WorkingDirectory { get; set; }
}
