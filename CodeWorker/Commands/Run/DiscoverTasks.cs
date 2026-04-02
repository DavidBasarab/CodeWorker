using System.Text.RegularExpressions;
using FatCat.CodeWorker.FileSystem;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IDiscoverTasks
{
	List<string> Discover(string todoFolderPath);
}

public class DiscoverTasks(IListFiles listFiles, ILogger logger) : IDiscoverTasks
{
	private static readonly Regex NumericPrefixPattern = new(@"^(\d+)", RegexOptions.Compiled);

	public List<string> Discover(string todoFolderPath)
	{
		logger.Information("Discovering tasks in {TodoFolder}", todoFolderPath);

		var files = listFiles.List(todoFolderPath);

		var taskFiles = files.Where(file => !Path.GetFileName(file).StartsWith(".")).ToList();

		var ordered = taskFiles
			.OrderBy(file => GetNumericPrefix(file) ?? int.MaxValue)
			.ThenBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
			.ToList();

		logger.Information("Found {TaskCount} tasks in {TodoFolder}", ordered.Count, todoFolderPath);

		return ordered;
	}

	private static int? GetNumericPrefix(string filePath)
	{
		var fileName = Path.GetFileName(filePath);
		var match = NumericPrefixPattern.Match(fileName);

		if (match.Success)
		{
			return int.Parse(match.Groups[1].Value);
		}

		return null;
	}
}
