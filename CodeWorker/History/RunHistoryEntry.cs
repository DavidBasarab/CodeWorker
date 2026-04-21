namespace FatCat.CodeWorker.History;

public class RunHistoryEntry
{
	public string Repository { get; set; }

	public string TaskName { get; set; }

	public DateTime Timestamp { get; set; }

	public bool Success { get; set; }
}
