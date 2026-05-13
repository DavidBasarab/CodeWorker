using System.Text;
using FatCat.Toolkit.Threading;
using Serilog;

namespace FatCat.CodeWorker.Claude;

public interface IClaudeTranscriptTailer
{
	Task<TailResult> Tail(TailRequest request, ClaudeProgressTracker tracker);
}

public class ClaudeTranscriptTailer(ITranscriptStream transcriptStream, IThread thread, IClock clock, ILogger logger)
	: IClaudeTranscriptTailer
{
	private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

	public async Task<TailResult> Tail(TailRequest request, ClaudeProgressTracker tracker)
	{
		var result = new TailResult();
		var leftover = new StringBuilder();
		var offset = 0L;
		var startedAt = clock.UtcNow;
		var lastHeartbeatAt = startedAt;

		logger.Information(
			"Starting transcript tailer for {TaskName} TranscriptPath={TranscriptPath} PollInterval={PollInterval} IdleTimeout={IdleTimeout} WallClock={WallClock}",
			request.TaskName,
			request.TranscriptPath,
			request.PollInterval,
			request.IdleTimeout,
			request.WallClockTimeout
		);

		while (true)
		{
			ConsumeNewBytes(request, tracker, result, leftover, ref offset);

			if (TryFinalizeFromOrchestratorDone(request, tracker, result, leftover, ref offset))
			{
				return result;
			}

			if (TryFinalizeFromResultEvent(tracker, result))
			{
				return result;
			}

			if (TryFinalizeFromDoneSentinel(request, tracker, result, leftover, ref offset))
			{
				return result;
			}

			var now = clock.UtcNow;

			if (HasWallClockExpired(request, startedAt, now))
			{
				logger.Error("Tailer wall-clock timeout for {TaskName} after {Elapsed}", request.TaskName, now - startedAt);

				result.StopReason = TailerStopReason.WallClockTimeout;
				result.ExitCode = -1;

				return result;
			}

			if (HasIdleTimeoutExpired(request, tracker, startedAt, now))
			{
				logger.Error(
					"Tailer idle timeout for {TaskName} — no event in {IdleTimeout}",
					request.TaskName,
					request.IdleTimeout
				);

				result.StopReason = TailerStopReason.IdleTimeout;
				result.ExitCode = -1;

				return result;
			}

			lastHeartbeatAt = LogHeartbeatIfDue(request, tracker, lastHeartbeatAt, now);

			await thread.Sleep(request.PollInterval);
		}
	}

	private void ConsumeNewBytes(
		TailRequest request,
		ClaudeProgressTracker tracker,
		TailResult result,
		StringBuilder leftover,
		ref long offset
	)
	{
		if (!transcriptStream.TranscriptExists(request.TranscriptPath))
		{
			return;
		}

		var chunk = transcriptStream.ReadFromOffset(request.TranscriptPath, offset, out var newOffset);

		offset = newOffset;

		if (string.IsNullOrEmpty(chunk))
		{
			return;
		}

		leftover.Append(chunk);

		ExtractCompletedLines(leftover, line => HandleLine(line, tracker, result));
	}

	private void HandleLine(string line, ClaudeProgressTracker tracker, TailResult result)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return;
		}

		result.RawOutputLines.Add(line);

		if (!ClaudeStreamEvent.TryParse(line, out var streamEvent))
		{
			logger.Debug("Non-JSON transcript line skipped: {Line}", line);

			return;
		}

		tracker.Record(streamEvent);
		result.Events.Add(streamEvent);

		LogStreamEvent(streamEvent);

		if (streamEvent.Kind == ClaudeEventKind.Result)
		{
			result.ResultEvent = streamEvent;
		}

		if (streamEvent.Kind == ClaudeEventKind.OrchestratorDone)
		{
			result.OrchestratorDoneEvent = streamEvent;

			if (streamEvent.ExitCode.HasValue)
			{
				result.ExitCode = streamEvent.ExitCode.Value;
			}
		}
	}

	private void LogStreamEvent(ClaudeStreamEvent streamEvent)
	{
		if (streamEvent.Kind == ClaudeEventKind.Result)
		{
			logger.Information(
				"Claude result event Subtype={Subtype} IsError={IsError} NumTurns={NumTurns} Duration={Duration}",
				streamEvent.Subtype,
				streamEvent.IsError,
				streamEvent.NumTurns,
				streamEvent.DurationMilliseconds
			);

			return;
		}

		if (streamEvent.Kind == ClaudeEventKind.OrchestratorDone)
		{
			logger.Information("Orchestrator-done event ExitCode={ExitCode}", streamEvent.ExitCode);

			return;
		}

		logger.Debug("Stream event Type={Type} Kind={Kind}", streamEvent.Type, streamEvent.Kind);
	}

	private bool TryFinalizeFromResultEvent(ClaudeProgressTracker tracker, TailResult result)
	{
		if (!tracker.ResultEventSeen)
		{
			return false;
		}

		if (tracker.OrchestratorDoneSeen)
		{
			return false;
		}

		result.StopReason = TailerStopReason.ResultEvent;

		return true;
	}

	private bool TryFinalizeFromOrchestratorDone(
		TailRequest request,
		ClaudeProgressTracker tracker,
		TailResult result,
		StringBuilder leftover,
		ref long offset
	)
	{
		if (!tracker.OrchestratorDoneSeen)
		{
			return false;
		}

		ConsumeNewBytes(request, tracker, result, leftover, ref offset);
		FlushTrailingLine(leftover, line => HandleLine(line, tracker, result));

		result.StopReason = TailerStopReason.OrchestratorDone;

		return true;
	}

	private bool TryFinalizeFromDoneSentinel(
		TailRequest request,
		ClaudeProgressTracker tracker,
		TailResult result,
		StringBuilder leftover,
		ref long offset
	)
	{
		if (string.IsNullOrEmpty(request.DoneSentinelPath))
		{
			return false;
		}

		if (!transcriptStream.DoneSentinelExists(request.DoneSentinelPath))
		{
			return false;
		}

		ConsumeNewBytes(request, tracker, result, leftover, ref offset);
		FlushTrailingLine(leftover, line => HandleLine(line, tracker, result));

		if (tracker.OrchestratorDoneSeen)
		{
			result.StopReason = TailerStopReason.OrchestratorDone;

			return true;
		}

		result.StopReason = TailerStopReason.OrchestratorDone;
		result.ExitCode = ReadSentinelExitCode(request.DoneSentinelPath);

		return true;
	}

	private int ReadSentinelExitCode(string sentinelPath)
	{
		try
		{
			var content = File.ReadAllText(sentinelPath).Trim();

			return int.TryParse(content, out var exitCode) ? exitCode : -1;
		}
		catch (Exception exception)
		{
			logger.Warning(exception, "Failed to read done sentinel {SentinelPath}", sentinelPath);

			return -1;
		}
	}

	private bool HasWallClockExpired(TailRequest request, DateTime startedAt, DateTime now)
	{
		if (request.WallClockTimeout <= TimeSpan.Zero)
		{
			return false;
		}

		return now - startedAt >= request.WallClockTimeout;
	}

	private bool HasIdleTimeoutExpired(TailRequest request, ClaudeProgressTracker tracker, DateTime startedAt, DateTime now)
	{
		if (request.IdleTimeout <= TimeSpan.Zero)
		{
			return false;
		}

		var lastEvent = tracker.TotalEvents > 0 ? tracker.LastEventAt : startedAt;

		return now - lastEvent >= request.IdleTimeout;
	}

	private DateTime LogHeartbeatIfDue(
		TailRequest request,
		ClaudeProgressTracker tracker,
		DateTime lastHeartbeatAt,
		DateTime now
	)
	{
		if (now - lastHeartbeatAt < HeartbeatInterval)
		{
			return lastHeartbeatAt;
		}

		logger.Information(
			"Tailer heartbeat for {TaskName} — events={Events}, assistant={Assistant}, toolUse={ToolUse}, toolResult={ToolResult}, lastEventAgo={LastEventAgo}",
			request.TaskName,
			tracker.TotalEvents,
			tracker.AssistantEvents,
			tracker.ToolUseEvents,
			tracker.ToolResultEvents,
			tracker.TimeSinceLastEvent()
		);

		return now;
	}

	private static void ExtractCompletedLines(StringBuilder leftover, Action<string> onLine)
	{
		var content = leftover.ToString();
		var lastNewline = content.LastIndexOf('\n');

		if (lastNewline < 0)
		{
			return;
		}

		var completed = content.Substring(0, lastNewline);
		var remainder = content.Substring(lastNewline + 1);

		leftover.Clear();
		leftover.Append(remainder);

		foreach (var line in completed.Split('\n'))
		{
			onLine(line.TrimEnd('\r'));
		}
	}

	private static void FlushTrailingLine(StringBuilder leftover, Action<string> onLine)
	{
		if (leftover.Length == 0)
		{
			return;
		}

		var line = leftover.ToString().TrimEnd('\r');

		leftover.Clear();

		onLine(line);
	}
}
