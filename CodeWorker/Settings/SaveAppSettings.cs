using System.Text.Json;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Settings;

public interface ISaveAppSettings
{
	Task Save(CodeWorkerSettings settings);
}

public class SaveAppSettings(IFileSystemTools fileSystemTools, ILogger logger) : ISaveAppSettings
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	public async Task Save(CodeWorkerSettings settings)
	{
		var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

		logger.Information("Saving app settings to {SettingsPath}", settingsPath);

		var json = JsonSerializer.Serialize(settings, JsonOptions);

		await fileSystemTools.WriteAllText(settingsPath, json);
	}
}
