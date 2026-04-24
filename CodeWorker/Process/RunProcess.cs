using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
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
	private const int ReadBufferSize = 4096;
	private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

	public async Task<ProcessResult> Run(ProcessSettings settings)
	{
		var result = new ProcessResult();

		using var process = new System.Diagnostics.Process();

		process.StartInfo = CreateStartInfo(settings);

		await using var liveLogWriter = OpenLiveLogWriter(settings);

		logger.Information("Starting process {FileName} in {WorkingDirectory}", settings.FileName, settings.WorkingDirectory);

		if (!TryStart(process, result, settings))
		{
			return result;
		}

		logger.Information("Process started, PID={ProcessId}", process.Id);

		await WriteStandardInput(process, settings);

		var streamState = new StreamReadState();

		var stdoutReader = StreamOutput(process.StandardOutput, result.OutputLines, "stdout", liveLogWriter, streamState);
		var stderrReader = StreamOutput(process.StandardError, result.ErrorLines, "stderr", liveLogWriter, streamState);

		using var heartbeatTokenSource = new CancellationTokenSource();

		var heartbeatTask = RunHeartbeat(process.Id, settings.FileName, streamState, heartbeatTokenSource.Token);

		await WaitForExit(process, result, settings);

		heartbeatTokenSource.Cancel();

		await heartbeatTask;

		await Task.WhenAll(stdoutReader, stderrReader);

		if (!result.TimedOut)
		{
			result.ExitCode = process.ExitCode;
		}

		logger.Information(
			"Process exited. ExitCode={ExitCode}, TimedOut={TimedOut}, stdoutBytes={StdoutBytes}, stderrBytes={StderrBytes}",
			result.ExitCode,
			result.TimedOut,
			streamState.StdoutBytes,
			streamState.StderrBytes
		);

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

	private StreamWriter OpenLiveLogWriter(ProcessSettings settings)
	{
		if (string.IsNullOrEmpty(settings.LiveLogPath))
		{
			return null;
		}

		try
		{
			var directory = Path.GetDirectoryName(settings.LiveLogPath);

			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			var fileStream = new FileStream(
				settings.LiveLogPath,
				FileMode.Append,
				FileAccess.Write,
				FileShare.Read,
				bufferSize: 4096,
				options: FileOptions.WriteThrough
			);

			return new StreamWriter(fileStream) { AutoFlush = true };
		}
		catch (Exception exception)
		{
			logger.Warning(exception, "Failed to open live log file {LiveLogPath}", settings.LiveLogPath);

			return null;
		}
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

	private async Task StreamOutput(
		StreamReader reader,
		List<string> lines,
		string streamName,
		StreamWriter liveLogWriter,
		StreamReadState streamState
	)
	{
		var buffer = new char[ReadBufferSize];
		var pending = new StringBuilder();

		while (true)
		{
			var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);

			if (charsRead <= 0)
			{
				break;
			}

			var chunk = new string(buffer, 0, charsRead);

			streamState.RecordChunk(streamName, charsRead);

			LogChunk(streamName, chunk);
			WriteLiveLog(liveLogWriter, streamName, chunk);

			pending.Append(chunk);

			ExtractCompletedLines(pending, lines);
		}

		FlushRemainingLine(pending, lines);
	}

	private void LogChunk(string streamName, string chunk)
	{
		if (streamName == "stderr")
		{
			logger.Warning("[{Stream}] {Chunk}", streamName, chunk);

			return;
		}

		logger.Information("[{Stream}] {Chunk}", streamName, chunk);
	}

	private void WriteLiveLog(StreamWriter liveLogWriter, string streamName, string chunk)
	{
		if (liveLogWriter == null)
		{
			return;
		}

		try
		{
			liveLogWriter.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{streamName}] {chunk}");
		}
		catch (Exception exception)
		{
			logger.Warning(exception, "Failed to write to live log");
		}
	}

	private void ExtractCompletedLines(StringBuilder pending, List<string> lines)
	{
		var content = pending.ToString();
		var lastNewline = content.LastIndexOf('\n');

		if (lastNewline < 0)
		{
			return;
		}

		var completed = content.Substring(0, lastNewline);

		foreach (var line in completed.Split('\n'))
		{
			lines.Add(line.TrimEnd('\r'));
		}

		pending.Clear();

		if (lastNewline + 1 < content.Length)
		{
			pending.Append(content, lastNewline + 1, content.Length - lastNewline - 1);
		}
	}

	private void FlushRemainingLine(StringBuilder pending, List<string> lines)
	{
		if (pending.Length == 0)
		{
			return;
		}

		lines.Add(pending.ToString().TrimEnd('\r'));
	}

	private async Task RunHeartbeat(
		int processId,
		string fileName,
		StreamReadState streamState,
		CancellationToken cancellationToken
	)
	{
		var elapsed = Stopwatch.StartNew();

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(HeartbeatInterval, cancellationToken);

				logger.Information(
					"Still waiting on {FileName} (PID={ProcessId}) — elapsed {Elapsed}, stdoutBytes={StdoutBytes}, stderrBytes={StderrBytes}, lastReadAgo={LastReadAgo}",
					fileName,
					processId,
					elapsed.Elapsed,
					streamState.StdoutBytes,
					streamState.StderrBytes,
					streamState.TimeSinceLastChunk()
				);
			}
		}
		catch (OperationCanceledException)
		{
			// ignored — heartbeat cancellation is expected when the process exits
		}
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

	private class StreamReadState
	{
		private readonly object syncRoot = new();
		private DateTime lastChunkAt = DateTime.UtcNow;

		public long StdoutBytes { get; private set; }

		public long StderrBytes { get; private set; }

		public void RecordChunk(string streamName, int charsRead)
		{
			lock (syncRoot)
			{
				if (streamName == "stdout")
				{
					StdoutBytes += charsRead;
				}
				else
				{
					StderrBytes += charsRead;
				}

				lastChunkAt = DateTime.UtcNow;
			}
		}

		public TimeSpan TimeSinceLastChunk()
		{
			lock (syncRoot)
			{
				return DateTime.UtcNow - lastChunkAt;
			}
		}
	}
}
