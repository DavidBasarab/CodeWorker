using System.Diagnostics.CodeAnalysis;

namespace FatCat.CodeWorker.FileSystem;

public interface IMoveFile
{
	void Move(string sourcePath, string destinationPath);
}

[ExcludeFromCodeCoverage(Justification = "Direct wrapper over File.Move — no business logic.")]
public class MoveFile : IMoveFile
{
	public void Move(string sourcePath, string destinationPath)
	{
		File.Move(sourcePath, destinationPath);
	}
}
