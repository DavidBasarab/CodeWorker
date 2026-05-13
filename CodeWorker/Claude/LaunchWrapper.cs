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
		WriteClaudeArgsFile(settings);

		var arguments = BuildArguments(settings);

		logger.Information(
			"Launching pwsh wrapper Script={ScriptPath} Transcript={TranscriptFile} WrapperLog={WrapperLogFile}",
			settings.ScriptPath,
			settings.TranscriptFile,
			settings.WrapperLogFile
		);

		var startInfo = new System.Diagnostics.ProcessStartInfo
		{
			FileName = "pwsh",
			Arguments = arguments,
			UseShellExecute = false,
			CreateNoWindow = true,
			WorkingDirectory = settings.WorkingDirectory ?? string.Empty,
		};

		var process = System.Diagnostics.Process.Start(startInfo);

		if (process == null)
		{
			throw new InvalidOperationException("Failed to launch pwsh wrapper — Process.Start returned null");
		}

		var processId = process.Id;

		logger.Information("Wrapper started PID={ProcessId}", processId);

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
			"-WrapperStartedFile",
			Quote(settings.WrapperStartedFile),
			"-WrapperLogFile",
			Quote(settings.WrapperLogFile),
		};

		parts.Add("-ClaudeArgsFile");
		parts.Add(Quote(settings.ClaudeArgsFile));

		return string.Join(" ", parts);
	}

	private static void WriteClaudeArgsFile(WrapperLaunchSettings settings)
	{
		if (string.IsNullOrEmpty(settings.ClaudeArgsFile))
		{
			return;
		}

		var lines = settings.ClaudeArgs ?? [];

		File.WriteAllLines(settings.ClaudeArgsFile, lines, System.Text.Encoding.UTF8);
	}

	private static string Quote(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "\"\"";
		}

		var escaped = value.Replace("\"", "\\\"");

		return $"\"{escaped}\"";
	}
}
