using FatCat.CodeWorker.History;
using Serilog;

namespace FatCat.CodeWorker.Commands.Info;

public interface IRunInfoCommand : ICommand { }

public class InfoCommand(ILoadRunHistory loadRunHistory, ILogger logger) : IRunInfoCommand
{
	private const int DefaultCount = 10;

	public async Task Execute(string[] args)
	{
		var count = ResolveCount(args);

		var history = await loadRunHistory.Load();

		if (history.Count == 0)
		{
			logger.Information("No run history found");

			return;
		}

		var entries = history.Skip(Math.Max(0, history.Count - count)).ToList();

		logger.Information("Last {Count} runs:", entries.Count);

		foreach (var entry in entries)
		{
			var status = entry.Success ? "success" : "failure";

			logger.Information(
				"  {Timestamp:yyyy-MM-dd HH:mm:ss} [{Status}] {Repository} — {TaskName}",
				entry.Timestamp,
				status,
				entry.Repository,
				entry.TaskName
			);
		}
	}

	private static int ResolveCount(string[] args)
	{
		if (args.Length <= 1)
		{
			return DefaultCount;
		}

		if (int.TryParse(args[1], out var parsed) && parsed > 0)
		{
			return parsed;
		}

		return DefaultCount;
	}
}
