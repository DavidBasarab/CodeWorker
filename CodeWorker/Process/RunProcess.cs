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

		using var process = new System.Diagnostics.Process();

		process.StartInfo = CreateStartInfo(settings);

		WireOutputHandlers(process, result);

		if (!TryStart(process, result, settings))
		{
			return result;
		}

		await WriteStandardInput(process, settings);

		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		await WaitForExit(process, result, settings);

		if (!result.TimedOut)
		{
			result.ExitCode = process.ExitCode;
		}

		return result;
	}

	private ProcessStartInfo CreateStartInfo(ProcessSettings settings)
	{
		return new ProcessStartInfo
		{
			FileName = settings.FileName,
			Arguments = settings.Arguments,
			WorkingDirectory = settings.WorkingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = !string.IsNullOrEmpty(settings.StandardInput),
		};
	}

	private void WireOutputHandlers(System.Diagnostics.Process process, ProcessResult result)
	{
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
	}

	private bool TryStart(System.Diagnostics.Process process, ProcessResult result, ProcessSettings settings)
	{
		try
		{
			process.Start();

			return true;
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to start process {FileName}", settings.FileName);

			result.FailedToStart = true;
			result.ExitCode = -1;
			result.ErrorLines.Add($"Failed to start process {settings.FileName}: {exception.Message}");

			return false;
		}
	}

	private async Task WriteStandardInput(System.Diagnostics.Process process, ProcessSettings settings)
	{
		if (string.IsNullOrEmpty(settings.StandardInput))
		{
			return;
		}

		await process.StandardInput.WriteAsync(settings.StandardInput);
		process.StandardInput.Close();
	}

	private async Task WaitForExit(System.Diagnostics.Process process, ProcessResult result, ProcessSettings settings)
	{
		if (settings.TimeoutMilliseconds <= 0)
		{
			await process.WaitForExitAsync();

			return;
		}

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
		}
	}
}
