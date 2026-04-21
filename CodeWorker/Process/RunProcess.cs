using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Serilog;

namespace FatCat.CodeWorker.Process;

public interface IRunProcess
{
	Task<ProcessResult> Run(ProcessSettings settings);
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over System.Diagnostics.Process — no business logic, tested via IRunProcess fakes in consuming classes."
)]
public class RunProcess(ILogger logger) : IRunProcess
{
	public async Task<ProcessResult> Run(ProcessSettings settings)
	{
		var result = new ProcessResult();

		var startInfo = new ProcessStartInfo
		{
			FileName = settings.FileName,
			Arguments = settings.Arguments,
			WorkingDirectory = settings.WorkingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		using var process = new System.Diagnostics.Process();

		process.StartInfo = startInfo;

		process.OutputDataReceived += (sender, args) =>
		{
			if (args.Data == null)
			{
				return;
			}

			logger.Information("[stdout] {OutputLine}", args.Data);
			result.OutputLines.Add(args.Data);
		};

		process.ErrorDataReceived += (sender, args) =>
		{
			if (args.Data == null)
			{
				return;
			}

			logger.Warning("[stderr] {ErrorLine}", args.Data);
			result.ErrorLines.Add(args.Data);
		};

		if (!string.IsNullOrEmpty(settings.StandardInput))
		{
			startInfo.RedirectStandardInput = true;
		}

		try
		{
			process.Start();
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to start process {FileName}", settings.FileName);

			result.FailedToStart = true;
			result.ExitCode = -1;
			result.ErrorLines.Add($"Failed to start process {settings.FileName}: {exception.Message}");

			return result;
		}

		if (!string.IsNullOrEmpty(settings.StandardInput))
		{
			await process.StandardInput.WriteAsync(settings.StandardInput);
			process.StandardInput.Close();
		}

		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		if (settings.TimeoutMilliseconds > 0)
		{
			using var cancellationTokenSource = new CancellationTokenSource(settings.TimeoutMilliseconds);

			try
			{
				await process.WaitForExitAsync(cancellationTokenSource.Token);
			}
			catch (OperationCanceledException)
			{
				logger.Error(
					"Process timed out after {TimeoutMinutes} minutes, killing process",
					settings.TimeoutMilliseconds / 60000
				);
				process.Kill(entireProcessTree: true);

				result.TimedOut = true;
				result.ExitCode = -1;
				result.ErrorLines.Add($"Process timed out after {settings.TimeoutMilliseconds / 60000} minutes");

				return result;
			}
		}
		else
		{
			await process.WaitForExitAsync();
		}

		result.ExitCode = process.ExitCode;

		return result;
	}
}
