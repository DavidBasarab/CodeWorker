using FatCat.CodeWorker.Cli.Commands;

namespace Testing.FatCat.CodeWorker.Cli.Commands;

public class ProcessArgumentsTests
{
	private readonly IResolveCommand resolveCommand;
	private readonly ICommand resolvedCommand;
	private readonly ProcessArguments processArguments;

	public ProcessArgumentsTests()
	{
		resolveCommand = A.Fake<IResolveCommand>();
		resolvedCommand = A.Fake<ICommand>();

		A.CallTo(() => resolveCommand.Resolve(A<string[]>._)).Returns(resolvedCommand);

		processArguments = new ProcessArguments(resolveCommand);
	}

	[Fact]
	public async Task ResolveTheCommandFromArgs()
	{
		var args = new[] { "verify", "--intent", "intent.json" };

		await processArguments.Process(args);

		A.CallTo(() => resolveCommand.Resolve(args)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteTheResolvedCommand()
	{
		var args = new[] { "verify", "--intent", "intent.json" };

		await processArguments.Process(args);

		A.CallTo(() => resolvedCommand.Execute(args)).MustHaveHappenedOnceExactly();
	}
}
