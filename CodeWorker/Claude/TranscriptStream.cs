using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FatCat.CodeWorker.Claude;

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over System.IO.File and FileStream — no business logic, exercised by tailer integration through its ITranscriptStream fake."
)]
public class TranscriptStream : ITranscriptStream
{
	public bool TranscriptExists(string path)
	{
		return File.Exists(path);
	}

	public bool DoneSentinelExists(string path)
	{
		return File.Exists(path);
	}

	public long Length(string path)
	{
		var info = new FileInfo(path);

		return info.Exists ? info.Length : 0;
	}

	public string ReadFromOffset(string path, long offset, out long newOffset)
	{
		newOffset = offset;

		if (!File.Exists(path))
		{
			return string.Empty;
		}

		using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

		if (offset > fileStream.Length)
		{
			offset = 0;
		}

		fileStream.Seek(offset, SeekOrigin.Begin);

		using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);

		var content = reader.ReadToEnd();

		newOffset = fileStream.Position;

		return content;
	}
}
