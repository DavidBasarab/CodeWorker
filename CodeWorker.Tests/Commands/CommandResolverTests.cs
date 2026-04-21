using FatCat.CodeWorker.Commands;
using FatCat.CodeWorker.Commands.List;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Setup;
using FatCat.CodeWorker.Commands.Track;
using FatCat.CodeWorker.Commands.Untrack;

namespace Testing.FatCat.CodeWorker.Commands;

public class CommandResolverTests
{
	private readonly IRunSetupCommand setupCommand;
	private readonly IRunTaskCommand runTaskCommand;
	private readonly IRunTrackCommand trackCommand;
	private readonly IRunUntrackCommand untrackCommand;
	private readonly IRunListCommand listCommand;
	private readonly CommandResolver resolver;

	public CommandResolverTests()
	{
		setupCommand = A.Fake<IRunSetupCommand>();
		runTaskCommand = A.Fake<IRunTaskCommand>();
		trackCommand = A.Fake<IRunTrackCommand>();
		untrackCommand = A.Fake<IRunUntrackCommand>();
		listCommand = A.Fake<IRunListCommand>();

		resolver = new CommandResolver(setupCommand, runTaskCommand, trackCommand, untrackCommand, listCommand);
	}

	[Fact]
	public void ReturnSetupCommandWhenArgsContainSetup()
	{
		var result = resolver.Resolve(new[] { "setup" });

		result.Should().BeSameAs(setupCommand);
	}

	[Fact]
	public void ReturnSetupCommandCaseInsensitive()
	{
		var result = resolver.Resolve(new[] { "Setup" });

		result.Should().BeSameAs(setupCommand);
	}

	[Fact]
	public void ReturnSetupCommandUpperCase()
	{
		var result = resolver.Resolve(new[] { "SETUP" });

		result.Should().BeSameAs(setupCommand);
	}

	[Fact]
	public void ReturnRunTaskCommandWhenNoArgumentsProvided()
	{
		var result = resolver.Resolve(Array.Empty<string>());

		result.Should().BeSameAs(runTaskCommand);
	}

	[Fact]
	public void ReturnRunTaskCommandForAnyNonCommandArgument()
	{
		var result = resolver.Resolve(new[] { "some-random-thing" });

		result.Should().BeSameAs(runTaskCommand);
	}

	[Fact]
	public void ReturnTrackCommandWhenArgsContainTrack()
	{
		var result = resolver.Resolve(new[] { "track" });

		result.Should().BeSameAs(trackCommand);
	}

	[Fact]
	public void ReturnTrackCommandCaseInsensitive()
	{
		var result = resolver.Resolve(new[] { "TRACK" });

		result.Should().BeSameAs(trackCommand);
	}

	[Fact]
	public void ReturnUntrackCommandWhenArgsContainUntrack()
	{
		var result = resolver.Resolve(new[] { "untrack" });

		result.Should().BeSameAs(untrackCommand);
	}

	[Fact]
	public void ReturnUntrackCommandCaseInsensitive()
	{
		var result = resolver.Resolve(new[] { "UNTRACK" });

		result.Should().BeSameAs(untrackCommand);
	}

	[Fact]
	public void ReturnListCommandWhenArgsContainList()
	{
		var result = resolver.Resolve(new[] { "list" });

		result.Should().BeSameAs(listCommand);
	}

	[Fact]
	public void ReturnListCommandCaseInsensitive()
	{
		var result = resolver.Resolve(new[] { "List" });

		result.Should().BeSameAs(listCommand);
	}
}
