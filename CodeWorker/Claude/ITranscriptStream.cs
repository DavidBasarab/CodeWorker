namespace FatCat.CodeWorker.Claude;

public interface ITranscriptStream
{
	bool TranscriptExists(string path);

	bool DoneSentinelExists(string path);

	long Length(string path);

	string ReadFromOffset(string path, long offset, out long newOffset);
}
