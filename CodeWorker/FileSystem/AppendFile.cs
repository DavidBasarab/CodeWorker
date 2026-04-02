using System.Diagnostics.CodeAnalysis;

namespace FatCat.CodeWorker.FileSystem;

public interface IAppendFile
{
	Task Append(string filePath, string content);
}

[ExcludeFromCodeCoverage(Justification = "Direct wrapper over File.AppendAllTextAsync — no business logic.")]
public class AppendFile : IAppendFile
{
	public async Task Append(string filePath, string content)
	{
		await File.AppendAllTextAsync(filePath, content);
	}
}
