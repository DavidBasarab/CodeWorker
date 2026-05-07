namespace FatCat.CodeWorker.Claude;

public class ClaudeProgressTracker
{
	private readonly object syncRoot = new();
	private DateTime lastEventAt = DateTime.UtcNow;

	public int TotalEvents { get; private set; }

	public int AssistantEvents { get; private set; }

	public int ToolUseEvents { get; private set; }

	public int ToolResultEvents { get; private set; }

	public int SystemEvents { get; private set; }

	public int UserEvents { get; private set; }

	public bool ResultEventSeen { get; private set; }

	public bool OrchestratorDoneSeen { get; private set; }

	public DateTime LastEventAt
	{
		get
		{
			lock (syncRoot)
			{
				return lastEventAt;
			}
		}
	}

	public TimeSpan TimeSinceLastEvent()
	{
		lock (syncRoot)
		{
			return DateTime.UtcNow - lastEventAt;
		}
	}

	public void Record(ClaudeStreamEvent streamEvent)
	{
		lock (syncRoot)
		{
			TotalEvents++;
			lastEventAt = DateTime.UtcNow;

			switch (streamEvent.Kind)
			{
				case ClaudeEventKind.Assistant:
					AssistantEvents++;
					break;
				case ClaudeEventKind.ToolUse:
					ToolUseEvents++;
					break;
				case ClaudeEventKind.ToolResult:
					ToolResultEvents++;
					break;
				case ClaudeEventKind.System:
					SystemEvents++;
					break;
				case ClaudeEventKind.User:
					UserEvents++;
					break;
				case ClaudeEventKind.Result:
					ResultEventSeen = true;
					break;
				case ClaudeEventKind.OrchestratorDone:
					OrchestratorDoneSeen = true;
					break;
			}
		}
	}
}
