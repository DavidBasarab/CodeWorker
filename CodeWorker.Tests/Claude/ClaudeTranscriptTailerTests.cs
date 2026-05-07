using FatCat.CodeWorker.Claude;
using FatCat.Toolkit.Testing;
using Serilog;

namespace Testing.FatCat.CodeWorker.Claude;

public class ClaudeTranscriptTailerTests
{
	private readonly FakeTranscriptStream transcriptStream;
	private readonly FakeThread thread;
	private readonly TestClock clock;
	private readonly ILogger logger;
	private readonly ClaudeTranscriptTailer tailer;
	private readonly TailRequest request;
	private readonly ClaudeProgressTracker tracker;

	public ClaudeTranscriptTailerTests()
	{
		transcriptStream = new FakeTranscriptStream();
		thread = new FakeThread();
		clock = new TestClock();
		logger = A.Fake<ILogger>();

		request = new TailRequest
		{
			TaskName = "task.md",
			TranscriptPath = @"C:\tasks\task.transcript.jsonl",
			DoneSentinelPath = @"C:\tasks\task.done",
			LiveLogPath = @"C:\tasks\task.live.log",
			PollInterval = TimeSpan.FromMilliseconds(10),
			IdleTimeout = TimeSpan.FromMinutes(10),
			WallClockTimeout = TimeSpan.FromMinutes(60),
		};

		tracker = new ClaudeProgressTracker();

		tailer = new ClaudeTranscriptTailer(transcriptStream, thread, clock, logger);
	}

	[Fact]
	public async Task ReturnImmediatelyWhenOrchestratorDoneEventIsAlreadyPresent()
	{
		transcriptStream.AppendLine("{\"type\":\"orchestrator-done\",\"exitCode\":0}");

		var result = await tailer.Tail(request, tracker);

		result.StopReason.Should().Be(TailerStopReason.OrchestratorDone);
	}

	[Fact]
	public async Task ReturnExitCodeFromOrchestratorDoneEvent()
	{
		transcriptStream.AppendLine("{\"type\":\"orchestrator-done\",\"exitCode\":42}");

		var result = await tailer.Tail(request, tracker);

		result.ExitCode.Should().Be(42);
	}

	[Fact]
	public async Task ParseAllEventsFromTranscript()
	{
		transcriptStream.AppendLine("{\"type\":\"assistant\"}");
		transcriptStream.AppendLine("{\"type\":\"tool_use\"}");
		transcriptStream.AppendLine("{\"type\":\"orchestrator-done\",\"exitCode\":0}");

		var result = await tailer.Tail(request, tracker);

		result.Events.Should().HaveCount(3);
	}

	[Fact]
	public async Task IncrementTrackerCountersForAssistantEvents()
	{
		transcriptStream.AppendLine("{\"type\":\"assistant\"}");
		transcriptStream.AppendLine("{\"type\":\"assistant\"}");
		transcriptStream.AppendLine("{\"type\":\"orchestrator-done\",\"exitCode\":0}");

		await tailer.Tail(request, tracker);

		tracker.AssistantEvents.Should().Be(2);
	}

	[Fact]
	public async Task ReturnResultEventWhenSeen()
	{
		transcriptStream.AppendLine("{\"type\":\"result\",\"subtype\":\"success\"}");
		transcriptStream.AppendLine("{\"type\":\"orchestrator-done\",\"exitCode\":0}");

		var result = await tailer.Tail(request, tracker);

		result.ResultEvent.Should().NotBeNull();
		result.ResultEvent.Subtype.Should().Be("success");
	}

	[Fact]
	public async Task SkipNonJsonLines()
	{
		transcriptStream.AppendLine("not json garbage");
		transcriptStream.AppendLine("{\"type\":\"orchestrator-done\",\"exitCode\":0}");

		var result = await tailer.Tail(request, tracker);

		result.StopReason.Should().Be(TailerStopReason.OrchestratorDone);
		result.Events.Should().HaveCount(1);
	}

	[Fact]
	public async Task FinalizeWhenDoneSentinelExistsEvenWithoutDoneEvent()
	{
		transcriptStream.AppendLine("{\"type\":\"assistant\"}");
		transcriptStream.AddDoneSentinel("0");

		var result = await tailer.Tail(request, tracker);

		result.StopReason.Should().Be(TailerStopReason.OrchestratorDone);
	}

	[Fact]
	public async Task ReturnIdleTimeoutWhenNoEventsArrive()
	{
		clock.AutoAdvancePerRead = TimeSpan.FromMinutes(20);

		request.IdleTimeout = TimeSpan.FromMinutes(10);
		request.WallClockTimeout = TimeSpan.FromMinutes(60);

		var result = await tailer.Tail(request, tracker);

		result.StopReason.Should().Be(TailerStopReason.IdleTimeout);
	}

	[Fact]
	public async Task ReturnWallClockTimeoutWhenWallClockExceeded()
	{
		clock.AutoAdvancePerRead = TimeSpan.FromMinutes(120);

		request.IdleTimeout = TimeSpan.FromMinutes(180);
		request.WallClockTimeout = TimeSpan.FromMinutes(60);

		var result = await tailer.Tail(request, tracker);

		result.StopReason.Should().Be(TailerStopReason.WallClockTimeout);
	}

	[Fact]
	public async Task ReturnExitCodeNegativeOneOnIdleTimeout()
	{
		clock.AutoAdvancePerRead = TimeSpan.FromMinutes(20);

		request.IdleTimeout = TimeSpan.FromMinutes(10);
		request.WallClockTimeout = TimeSpan.FromMinutes(60);

		var result = await tailer.Tail(request, tracker);

		result.ExitCode.Should().Be(-1);
	}

	[Fact]
	public async Task HandlePartialLineThatCompletesOnNextPoll()
	{
		transcriptStream.AppendRaw("{\"type\":\"ass");
		transcriptStream.QueueAfterPoll("istant\"}\n{\"type\":\"orchestrator-done\",\"exitCode\":0}\n");

		var result = await tailer.Tail(request, tracker);

		result.StopReason.Should().Be(TailerStopReason.OrchestratorDone);
		tracker.AssistantEvents.Should().Be(1);
	}
}

public class TestClock : IClock
{
	public DateTime UtcNowValue { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	public TimeSpan AutoAdvancePerRead { get; set; } = TimeSpan.Zero;

	public DateTime UtcNow
	{
		get
		{
			var current = UtcNowValue;

			UtcNowValue += AutoAdvancePerRead;

			return current;
		}
	}

	public void Advance(TimeSpan delta)
	{
		UtcNowValue += delta;
	}
}

public class FakeTranscriptStream : ITranscriptStream
{
	private readonly object syncRoot = new();
	private string content = "";
	private string queuedAfterPoll;
	private string sentinelContent;
	private bool sentinelFlag;

	public void AppendLine(string line)
	{
		lock (syncRoot)
		{
			content += line + "\n";
		}
	}

	public void AppendRaw(string raw)
	{
		lock (syncRoot)
		{
			content += raw;
		}
	}

	public void QueueAfterPoll(string raw)
	{
		lock (syncRoot)
		{
			queuedAfterPoll = raw;
		}
	}

	public void AddDoneSentinel(string exitCodeString)
	{
		lock (syncRoot)
		{
			sentinelContent = exitCodeString;
			sentinelFlag = true;
		}
	}

	public bool TranscriptExists(string path)
	{
		lock (syncRoot)
		{
			return content.Length > 0;
		}
	}

	public bool DoneSentinelExists(string path)
	{
		lock (syncRoot)
		{
			return sentinelFlag;
		}
	}

	public long Length(string path)
	{
		lock (syncRoot)
		{
			return content.Length;
		}
	}

	public string ReadFromOffset(string path, long offset, out long newOffset)
	{
		lock (syncRoot)
		{
			if (offset >= content.Length)
			{
				newOffset = content.Length;

				if (queuedAfterPoll != null)
				{
					content += queuedAfterPoll;
					queuedAfterPoll = null;
				}

				return string.Empty;
			}

			var chunk = content.Substring((int)offset);

			newOffset = content.Length;

			return chunk;
		}
	}
}
