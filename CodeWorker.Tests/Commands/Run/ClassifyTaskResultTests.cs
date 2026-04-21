using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Process;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class ClassifyTaskResultTests
{
	private readonly ClassifyTaskResult classifyTaskResult;

	public ClassifyTaskResultTests()
	{
		classifyTaskResult = new ClassifyTaskResult();
	}

	[Fact]
	public void ReturnDoneWhenExitCodeIsZeroAndNoMarker()
	{
		var result = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "All good" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Done);
	}

	[Fact]
	public void ReturnFailedWhenProcessTimedOut()
	{
		var result = new ProcessResult { ExitCode = -1, TimedOut = true };

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Failed);
	}

	[Fact]
	public void ReturnFailedWhenProcessFailedToStart()
	{
		var result = new ProcessResult { ExitCode = -1, FailedToStart = true };

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Failed);
	}

	[Fact]
	public void ReturnFailedWhenTimedOutEvenIfOutputContainsBlockedMarker()
	{
		var result = new ProcessResult
		{
			ExitCode = -1,
			TimedOut = true,
			OutputLines = new List<string> { "BLOCKED: something" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Failed);
	}

	[Fact]
	public void ReturnBlockedWhenStdoutHasBlockedMarker()
	{
		var result = new ProcessResult
		{
			ExitCode = 1,
			OutputLines = new List<string> { "BLOCKED: missing dependency" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Blocked);
	}

	[Fact]
	public void ReturnBlockedWhenStderrHasBlockedMarker()
	{
		var result = new ProcessResult
		{
			ExitCode = 1,
			ErrorLines = new List<string> { "BLOCKED: cannot proceed" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Blocked);
	}

	[Fact]
	public void ReturnBlockedWhenMarkerIsLowercase()
	{
		var result = new ProcessResult
		{
			ExitCode = 1,
			OutputLines = new List<string> { "blocked: contradictory instructions" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Blocked);
	}

	[Fact]
	public void ReturnBlockedWhenMarkerIsMixedCase()
	{
		var result = new ProcessResult
		{
			ExitCode = 1,
			OutputLines = new List<string> { "BlOcKeD: contradictory instructions" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Blocked);
	}

	[Fact]
	public void ReturnFailedWhenExitCodeIsNonZeroAndNoMarker()
	{
		var result = new ProcessResult
		{
			ExitCode = 1,
			OutputLines = new List<string> { "Some output" },
			ErrorLines = new List<string> { "Some error" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Failed);
	}

	[Fact]
	public void ReturnDoneWhenExitCodeIsZeroEvenIfStderrHasContent()
	{
		var result = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "Done" },
			ErrorLines = new List<string> { "warning: deprecation" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Done);
	}

	[Fact]
	public void ReturnDoneWhenExitCodeIsZeroEvenIfOutputContainsBlockedMarker()
	{
		var result = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "BLOCKED: this was earlier discussion, task still completed" },
		};

		classifyTaskResult.Classify(result).Should().Be(TaskOutcome.Done);
	}
}
