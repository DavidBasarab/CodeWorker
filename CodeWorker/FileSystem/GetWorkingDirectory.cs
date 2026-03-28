using System.Diagnostics.CodeAnalysis;

namespace FatCat.CodeWorker.FileSystem;

public interface IGetWorkingDirectory
{
	string GetWorkingDirectory();
}

[ExcludeFromCodeCoverage(Justification = "Direct wrapper over Directory.GetCurrentDirectory — no business logic.")]
public class WorkingDirectoryProvider : IGetWorkingDirectory
{
	public string GetWorkingDirectory()
	{
		return Directory.GetCurrentDirectory();
	}
}
