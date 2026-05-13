using System.Text.Json;
using System.Text.Json.Serialization;
using FatCat.CodeWorker.FileSystem;
using Serilog;

namespace FatCat.CodeWorker.History;

public interface IRecordRepositoryRunHistory
{
	Task Record(string repositoryPath, RepositoryRunHistoryEntry entry);
}

public class RecordRepositoryRunHistory(IAppendFile appendFile, ILogger logger) : IRecordRepositoryRunHistory
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new JsonStringEnumConverter() },
	};

	public async Task Record(string repositoryPath, RepositoryRunHistoryEntry entry)
	{
		var historyPath = Path.Combine(repositoryPath, "tasks", "run-history.jsonl");

		var line = JsonSerializer.Serialize(entry, JsonOptions) + "\n";

		logger.Debug("Recording repository run history for {TaskName} to {HistoryPath}", entry.TaskName, historyPath);

		await appendFile.Append(historyPath, line);
	}
}
