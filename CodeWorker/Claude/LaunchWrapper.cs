using System.Diagnostics.CodeAnalysis;
using Serilog;

namespace FatCat.CodeWorker.Claude;

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over System.Diagnostics.Process.Start — no business logic, exercised end-to-end during real runs."
)]
public class LaunchWrapper(ILogger logger) : ILaunchWrapper
{
	public int Launch(WrapperLaunchSettings settings)
	{
		var arguments = BuildArguments(settings);

		logger.Information(
			"Launching detached pwsh wrapper Script={ScriptPath} Transcript={TranscriptFile}",
			settings.ScriptPath,
			settings.TranscriptFile
		);

		var startInfo = new System.Diagnostics.ProcessStartInfo
		{
			FileName = "pwsh",
			Arguments = arguments,
			UseShellExecute = true,
			CreateNoWindow = true,
			WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
			WorkingDirectory = settings.WorkingDirectory ?? string.Empty,
		};

		using var process = System.Diagnostics.Process.Start(startInfo);

		if (process == null)
		{
			throw new InvalidOperationException("Failed to launch pwsh wrapper — Process.Start returned null");
		}

		var processId = process.Id;

		logger.Information("Detached wrapper started PID={ProcessId}", processId);

		return processId;
	}

	private static string BuildArguments(WrapperLaunchSettings settings)
	{
		var parts = new List<string>
		{
			"-NoProfile",
			"-NonInteractive",
			"-WindowStyle",
			"Hidden",
			"-ExecutionPolicy",
			"Bypass",
			"-File",
			Quote(settings.ScriptPath),
			"-PromptFile",
			Quote(settings.PromptFile),
			"-TranscriptFile",
			Quote(settings.TranscriptFile),
			"-StderrFile",
			Quote(settings.StderrFile),
			"-DoneSentinel",
			Quote(settings.DoneSentinel),
			"-PidFile",
			Quote(settings.PidFile),
		};

		if (settings.ClaudeArgs is { Count: > 0 })
		{
			parts.Add("-ClaudeArgs");
			parts.Add(string.Join(",", settings.ClaudeArgs.Select(Quote)));
		}

		return string.Join(" ", parts);
	}

	private static string Quote(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "''";
		}

		var escaped = value.Replace("'", "''");

		return $"'{escaped}'";
	}
}
