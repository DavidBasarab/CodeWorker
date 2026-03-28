using System.Text.Json;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Settings;

public interface ILoadAppSettings
{
	Task<CodeWorkerSettings> Load();
}

public class LoadAppSettings(IFileSystemTools fileSystemTools, ILogger logger) : ILoadAppSettings
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	public async Task<CodeWorkerSettings> Load()
	{
		var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

		logger.Information("Loading app settings from {SettingsPath}", settingsPath);

		var json = await fileSystemTools.ReadAllText(settingsPath);

		return JsonSerializer.Deserialize<CodeWorkerSettings>(json, JsonOptions);
	}
}
