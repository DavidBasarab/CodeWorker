namespace FatCat.CodeWorker.Claude;

public interface ITranscriptPaths
{
	TranscriptPaths For(string markdownFilePath);
}

public class TranscriptPaths
{
	public string PromptFile { get; set; }

	public string TranscriptFile { get; set; }

	public string StderrFile { get; set; }

	public string DoneSentinel { get; set; }

	public string PidFile { get; set; }

	public string LiveLogFile { get; set; }

	public string TaskName { get; set; }
}
