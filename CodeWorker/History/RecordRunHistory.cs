using System.Text.Json;
using FatCat.CodeWorker.FileSystem;
using Serilog;

namespace FatCat.CodeWorker.History;

public interface IRecordRunHistory
{
	Task Record(RunHistoryEntry entry);
}

public class RecordRunHistory(IAppendFile appendFile, ILogger logger) : IRecordRunHistory
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

	public async Task Record(RunHistoryEntry entry)
	{
		var historyPath = Path.Combine(AppContext.BaseDirectory, "runs.jsonl");

		var line = JsonSerializer.Serialize(entry, JsonOptions) + "\n";

		logger.Debug("Recording run history entry for {TaskName} to {HistoryPath}", entry.TaskName, historyPath);

		await appendFile.Append(historyPath, line);
	}
}
