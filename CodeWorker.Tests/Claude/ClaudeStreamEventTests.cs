using FatCat.CodeWorker.Claude;

namespace Testing.FatCat.CodeWorker.Claude;

public class ClaudeStreamEventTests
{
	[Fact]
	public void ParseAssistantEvent()
	{
		var json = "{\"type\":\"assistant\",\"message\":\"hello\"}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.Assistant);
	}

	[Fact]
	public void ParseToolUseEvent()
	{
		var json = "{\"type\":\"tool_use\",\"name\":\"Read\"}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.ToolUse);
	}

	[Fact]
	public void ParseToolResultEvent()
	{
		var json = "{\"type\":\"tool_result\",\"is_error\":false}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.ToolResult);
	}

	[Fact]
	public void ParseSystemEvent()
	{
		var json = "{\"type\":\"system\",\"subtype\":\"init\"}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.System);
		streamEvent.Subtype.Should().Be("init");
	}

	[Fact]
	public void ParseUserEvent()
	{
		var json = "{\"type\":\"user\"}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.User);
	}

	[Fact]
	public void ParseResultSuccessEvent()
	{
		var json =
			"{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"num_turns\":3,\"duration_ms\":1234.5,\"result\":\"all good\"}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.Result);
		streamEvent.Subtype.Should().Be("success");
		streamEvent.IsError.Should().BeFalse();
		streamEvent.NumTurns.Should().Be(3);
		streamEvent.DurationMilliseconds.Should().Be(1234.5);
		streamEvent.ResultText.Should().Be("all good");
	}

	[Fact]
	public void ParseResultErrorEvent()
	{
		var json = "{\"type\":\"result\",\"subtype\":\"error_max_turns\",\"is_error\":true}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.Result);
		streamEvent.Subtype.Should().Be("error_max_turns");
		streamEvent.IsError.Should().BeTrue();
	}

	[Fact]
	public void ParseOrchestratorDoneEvent()
	{
		var json = "{\"type\":\"orchestrator-done\",\"exitCode\":0,\"endedAt\":\"2026-05-06T21:30:00\"}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.OrchestratorDone);
		streamEvent.ExitCode.Should().Be(0);
	}

	[Fact]
	public void ParseOrchestratorDoneWithNonZeroExitCode()
	{
		var json = "{\"type\":\"orchestrator-done\",\"exitCode\":42}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.ExitCode.Should().Be(42);
	}

	[Fact]
	public void ParseUnknownTypeAsUnknownKind()
	{
		var json = "{\"type\":\"something_else\"}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.Kind.Should().Be(ClaudeEventKind.Unknown);
	}

	[Fact]
	public void ReturnFalseForInvalidJson()
	{
		ClaudeStreamEvent.TryParse("not json", out _).Should().BeFalse();
	}

	[Fact]
	public void ReturnFalseForEmptyString()
	{
		ClaudeStreamEvent.TryParse("", out _).Should().BeFalse();
	}

	[Fact]
	public void ReturnFalseForWhitespaceOnly()
	{
		ClaudeStreamEvent.TryParse("   ", out _).Should().BeFalse();
	}

	[Fact]
	public void ReturnFalseForJsonArray()
	{
		ClaudeStreamEvent.TryParse("[1,2,3]", out _).Should().BeFalse();
	}

	[Fact]
	public void PreserveRawJsonOnTheEvent()
	{
		var json = "{\"type\":\"assistant\"}";

		ClaudeStreamEvent.TryParse(json, out var streamEvent).Should().BeTrue();
		streamEvent.RawJson.Should().Be(json);
	}
}
