using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace FatCat.CodeWorker.Commands;

public interface IResolveRepositoryPath
{
	Task<string> Resolve(string[] args);
}

public class ResolveRepositoryPath(ILoadAppSettings loadAppSettings, IGetWorkingDirectory getWorkingDirectory, ILogger logger)
	: IResolveRepositoryPath
{
	public async Task<string> Resolve(string[] args)
	{
		if (args.Length <= 1)
		{
			return getWorkingDirectory.GetWorkingDirectory();
		}

		var argument = args[1];

		if (PathLooksAbsolute(argument))
		{
			return argument;
		}

		var settings = await loadAppSettings.Load();

		var matchedPath = FindTrackedPathByFolderName(settings, argument);

		if (matchedPath != null)
		{
			return matchedPath;
		}

		logger.Warning("No tracked repository found matching name {Name}", argument);

		return argument;
	}

	private static bool PathLooksAbsolute(string argument)
	{
		return Path.IsPathRooted(argument);
	}

	private static string FindTrackedPathByFolderName(CodeWorkerSettings settings, string name)
	{
		if (settings.Repositories == null)
		{
			return null;
		}

		return settings
			.Repositories.Select(repository => repository.Path)
			.FirstOrDefault(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));
	}
}
