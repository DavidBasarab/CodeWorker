using FatCat.CodeWorker.FileSystem;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface ICollectReferenceFiles
{
	Task<List<ReferenceFile>> Collect(string referenceFolder);
}

public class CollectReferenceFiles(IFileSystemTools fileSystemTools, IListFiles listFiles, ILogger logger)
	: ICollectReferenceFiles
{
	public async Task<List<ReferenceFile>> Collect(string referenceFolder)
	{
		if (!fileSystemTools.DirectoryExists(referenceFolder))
		{
			return new List<ReferenceFile>();
		}

		var files = listFiles.List(referenceFolder);

		if (files.Length == 0)
		{
			return new List<ReferenceFile>();
		}

		var referenceFiles = new List<ReferenceFile>();

		foreach (var filePath in files)
		{
			var content = await fileSystemTools.ReadAllText(filePath);

			referenceFiles.Add(new ReferenceFile { Name = Path.GetFileName(filePath), Content = content });
		}

		logger.Information("Found {Count} reference file(s) in {Folder}", referenceFiles.Count, referenceFolder);

		return referenceFiles;
	}
}
