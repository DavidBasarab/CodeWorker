using System.Diagnostics.CodeAnalysis;

namespace FatCat.CodeWorker.FileSystem;

public interface IWriteFile
{
	Task Write(string filePath, string content);
}

[ExcludeFromCodeCoverage(Justification = "Direct wrapper over File.WriteAllTextAsync — no business logic.")]
public class WriteFile : IWriteFile
{
	public async Task Write(string filePath, string content)
	{
		await File.WriteAllTextAsync(filePath, content);
	}
}
