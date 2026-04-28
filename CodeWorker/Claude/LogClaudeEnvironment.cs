using FatCat.CodeWorker.Process;
using Serilog;

namespace FatCat.CodeWorker.Claude;

public interface ILogClaudeEnvironment
{
	Task Log();
}

public class LogClaudeEnvironment(IRunProcess runProcess, ILogger logger) : ILogClaudeEnvironment
{
	private const int DiagnosticTimeoutMilliseconds = 30_000;

	public async Task Log()
	{
		logger.Information("Collecting Claude environment diagnostics");

		await RunDiagnostic("--version");
	}

	private async Task RunDiagnostic(string arguments)
	{
		var settings = new ProcessSettings
		{
			FileName = "claude",
			Arguments = arguments,
			TimeoutMilliseconds = DiagnosticTimeoutMilliseconds,
		};

		var result = await runProcess.Run(settings);

		var output = string.Join(Environment.NewLine, result.OutputLines);

		if (result.FailedToStart || result.ExitCode != 0)
		{
			var errors = string.Join(Environment.NewLine, result.ErrorLines);

			logger.Warning(
				"claude {Arguments} diagnostic failed — ExitCode={ExitCode}, Output={Output}, Errors={Errors}",
				arguments,
				result.ExitCode,
				output,
				errors
			);

			return;
		}

		logger.Information("claude {Arguments} — ExitCode={ExitCode}, Output={Output}", arguments, result.ExitCode, output);
	}
}
