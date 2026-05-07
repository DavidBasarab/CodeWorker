using System.Text.Json;
using System.Text.Json.Nodes;

namespace FatCat.CodeWorker.Claude;

public class ClaudeStreamEvent
{
	public ClaudeEventKind Kind { get; set; }

	public string Type { get; set; }

	public string Subtype { get; set; }

	public bool IsError { get; set; }

	public string ResultText { get; set; }

	public int? ExitCode { get; set; }

	public int? NumTurns { get; set; }

	public double? DurationMilliseconds { get; set; }

	public string RawJson { get; set; }

	public JsonNode Node { get; set; }

	public static bool TryParse(string line, out ClaudeStreamEvent streamEvent)
	{
		streamEvent = null;

		if (string.IsNullOrWhiteSpace(line))
		{
			return false;
		}

		JsonNode node;

		try
		{
			node = JsonNode.Parse(line);
		}
		catch (JsonException)
		{
			return false;
		}

		if (node is not JsonObject obj)
		{
			return false;
		}

		var type = obj["type"]?.GetValue<string>();

		streamEvent = new ClaudeStreamEvent
		{
			Type = type,
			Kind = MapKind(type),
			Subtype = obj["subtype"]?.GetValue<string>(),
			IsError = obj["is_error"]?.GetValue<bool>() ?? false,
			ResultText = obj["result"]?.GetValue<string>(),
			NumTurns = TryGetInt(obj, "num_turns"),
			DurationMilliseconds = TryGetDouble(obj, "duration_ms"),
			ExitCode = TryGetInt(obj, "exitCode"),
			RawJson = line,
			Node = node,
		};

		return true;
	}

	private static ClaudeEventKind MapKind(string type)
	{
		return type switch
		{
			"system" => ClaudeEventKind.System,
			"assistant" => ClaudeEventKind.Assistant,
			"user" => ClaudeEventKind.User,
			"tool_use" => ClaudeEventKind.ToolUse,
			"tool_result" => ClaudeEventKind.ToolResult,
			"result" => ClaudeEventKind.Result,
			"orchestrator-done" => ClaudeEventKind.OrchestratorDone,
			"orchestrator-started" => ClaudeEventKind.OrchestratorStarted,
			_ => ClaudeEventKind.Unknown,
		};
	}

	private static int? TryGetInt(JsonObject obj, string property)
	{
		var value = obj[property];

		if (value == null)
		{
			return null;
		}

		try
		{
			return value.GetValue<int>();
		}
		catch (FormatException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	private static double? TryGetDouble(JsonObject obj, string property)
	{
		var value = obj[property];

		if (value == null)
		{
			return null;
		}

		try
		{
			return value.GetValue<double>();
		}
		catch (FormatException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}
}

public enum ClaudeEventKind
{
	Unknown,
	System,
	Assistant,
	User,
	ToolUse,
	ToolResult,
	Result,
	OrchestratorStarted,
	OrchestratorDone,
}
