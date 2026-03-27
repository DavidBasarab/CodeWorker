using FatCat.CodeWorker.Commands;
using Serilog;

namespace Testing.FatCat.CodeWorker;

public class CodeWorkerApplicationTests
{
	private readonly IResolveCommand resolveCommand;
	private readonly ICommand resolvedCommand;
	private readonly ILogger logger;
	private readonly CodeWorkerApplication application;

	public CodeWorkerApplicationTests()
	{
		resolveCommand = A.Fake<IResolveCommand>();
		resolvedCommand = A.Fake<ICommand>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => resolveCommand.Resolve(A<string[]>.Ignored)).Returns(resolvedCommand);

		application = new CodeWorkerApplication(resolveCommand, logger);
	}

	[Fact]
	public async Task ResolveTheCommandFromArgs()
	{
		var args = new[] { "setup", @"C:\Projects\my-api" };

		await application.DoWork(args);

		A.CallTo(() => resolveCommand.Resolve(args)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteTheResolvedCommand()
	{
		var args = new[] { "setup", @"C:\Projects\my-api" };

		await application.DoWork(args);

		A.CallTo(() => resolvedCommand.Execute(args)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWelcomeMessage()
	{
		await application.DoWork(new[] { "setup" });

		A.CallTo(() => logger.Information("Welcome to Code Worker")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogErrorWhenNoArgumentsProvided()
	{
		await application.DoWork(Array.Empty<string>());

		A.CallTo(() => logger.Error("No arguments provided. Usage: CodeWorker <command> [options]"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotResolveCommandWhenNoArgumentsProvided()
	{
		await application.DoWork(Array.Empty<string>());

		A.CallTo(() => resolveCommand.Resolve(A<string[]>.Ignored)).MustNotHaveHappened();
	}
}
