using FatCat.CodeWorker.Commands;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Setup;

namespace Testing.FatCat.CodeWorker.Commands;

public class CommandResolverTests
{
	private readonly IRunSetupCommand setupCommand;
	private readonly IRunTaskCommand runTaskCommand;
	private readonly CommandResolver resolver;

	public CommandResolverTests()
	{
		setupCommand = A.Fake<IRunSetupCommand>();
		runTaskCommand = A.Fake<IRunTaskCommand>();

		resolver = new CommandResolver(setupCommand, runTaskCommand);
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
	public void ReturnRunTaskCommandForUnknownCommand()
	{
		var result = resolver.Resolve(new[] { @"C:\Tasks\my-task.md" });

		result.Should().BeSameAs(runTaskCommand);
	}

	[Fact]
	public void ReturnRunTaskCommandForAnyNonCommandArgument()
	{
		var result = resolver.Resolve(new[] { "some-random-thing" });

		result.Should().BeSameAs(runTaskCommand);
	}
}
