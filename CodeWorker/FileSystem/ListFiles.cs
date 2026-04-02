using System.Diagnostics.CodeAnalysis;

namespace FatCat.CodeWorker.FileSystem;

public interface IListFiles
{
	string[] List(string directoryPath);
}

[ExcludeFromCodeCoverage(Justification = "Direct wrapper over Directory.GetFiles — no business logic.")]
public class ListFiles : IListFiles
{
	public string[] List(string directoryPath)
	{
		return Directory.GetFiles(directoryPath);
	}
}
