using FatCat.CodeWorker.FileSystem;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IMoveTask
{
	void Move(string sourceFilePath, string destinationFolder);
}

public class MoveTask(IMoveFile moveFile, ILogger logger) : IMoveTask
{
	public void Move(string sourceFilePath, string destinationFolder)
	{
		var fileName = Path.GetFileName(sourceFilePath);
		var destinationPath = Path.Combine(destinationFolder, fileName);

		logger.Information("Moving task {FileName} to {DestinationFolder}", fileName, destinationFolder);

		moveFile.Move(sourceFilePath, destinationPath);
	}
}
