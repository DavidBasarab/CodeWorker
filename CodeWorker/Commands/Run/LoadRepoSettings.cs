using System.Text.Json;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface ILoadRepoSettings
{
	Task<RepoSettings> Load(string repositoryPath);
}

public class LoadRepoSettings(IFileSystemTools fileSystemTools, ILogger logger) : ILoadRepoSettings
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	public async Task<RepoSettings> Load(string repositoryPath)
	{
		var settingsPath = Path.Combine(repositoryPath, "tasks", "settings.json");

		logger.Information("Loading repository settings from {SettingsPath}", settingsPath);

		var json = await fileSystemTools.ReadAllText(settingsPath);

		return JsonSerializer.Deserialize<RepoSettings>(json, JsonOptions);
	}
}
