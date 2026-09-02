using FatCat.CodeWorker.Cli.Commands;
using FatCat.CodeWorker.Cli.Commands.Verify;

namespace Testing.FatCat.CodeWorker.Cli.Commands;

public class CommandResolverTests
{
	private readonly IRunVerifyCommand verifyCommand;
	private readonly CommandResolver commandResolver;

	public CommandResolverTests()
	{
		verifyCommand = A.Fake<IRunVerifyCommand>();

		commandResolver = new CommandResolver(verifyCommand);
	}

	[Fact]
	public void ResolveTheVerifyCommandForTheVerifyVerb()
	{
		var resolved = commandResolver.Resolve(["verify"]);

		resolved.Should().Be(verifyCommand);
	}

	[Fact]
	public void ResolveTheVerifyCommandCaseInsensitively()
	{
		var resolved = commandResolver.Resolve(["VERIFY"]);

		resolved.Should().Be(verifyCommand);
	}

	[Fact]
	public void ResolveTheVerifyCommandForAnUnknownVerb()
	{
		var resolved = commandResolver.Resolve(["nonsense"]);

		resolved.Should().Be(verifyCommand);
	}

	[Fact]
	public void ResolveTheVerifyCommandWhenNoArgumentsProvided()
	{
		var resolved = commandResolver.Resolve([]);

		resolved.Should().Be(verifyCommand);
	}
}
