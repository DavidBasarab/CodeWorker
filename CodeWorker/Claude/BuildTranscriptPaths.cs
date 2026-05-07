namespace FatCat.CodeWorker.Claude;

public class BuildTranscriptPaths : ITranscriptPaths
{
	public TranscriptPaths For(string markdownFilePath)
	{
		var directory = Path.GetDirectoryName(markdownFilePath) ?? string.Empty;
		var taskName = Path.GetFileName(markdownFilePath);
		var withoutExtension = Path.GetFileNameWithoutExtension(markdownFilePath);

		return new TranscriptPaths
		{
			TaskName = taskName,
			PromptFile = Path.Combine(directory, $"{withoutExtension}.prompt.txt"),
			TranscriptFile = Path.Combine(directory, $"{withoutExtension}.transcript.jsonl"),
			StderrFile = Path.Combine(directory, $"{withoutExtension}.stderr.log"),
			DoneSentinel = Path.Combine(directory, $"{withoutExtension}.done"),
			PidFile = Path.Combine(directory, $"{withoutExtension}.wrapper.pid"),
			LiveLogFile = Path.Combine(directory, $"{withoutExtension}.live.log"),
		};
	}
}
