using System.Text.Json;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.History;

public interface ILoadRunHistory
{
	Task<List<RunHistoryEntry>> Load();
}

public class LoadRunHistory(IFileSystemTools fileSystemTools, ILogger logger) : ILoadRunHistory
{
	public async Task<List<RunHistoryEntry>> Load()
	{
		var historyPath = Path.Combine(AppContext.BaseDirectory, "runs.jsonl");

		if (!fileSystemTools.FileExists(historyPath))
		{
			logger.Debug("Run history file not found at {HistoryPath}", historyPath);

			return new List<RunHistoryEntry>();
		}

		var lines = await fileSystemTools.ReadAllLines(historyPath);

		return [.. lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(DeserializeLine)];
	}

	private static RunHistoryEntry DeserializeLine(string line)
	{
		return JsonSerializer.Deserialize<RunHistoryEntry>(line);
	}
}
