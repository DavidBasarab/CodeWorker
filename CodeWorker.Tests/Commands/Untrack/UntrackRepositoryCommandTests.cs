using FatCat.CodeWorker.Commands;
using FatCat.CodeWorker.Commands.Untrack;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Untrack;

public class UntrackRepositoryCommandTests
{
	private readonly IUntrackRepository untrackRepository;
	private readonly IResolveRepositoryPath resolveRepositoryPath;
	private readonly ILogger logger;
	private readonly UntrackRepositoryCommand command;
	private readonly string resolvedPath = @"C:\Projects\my-api";

	public UntrackRepositoryCommandTests()
	{
		untrackRepository = A.Fake<IUntrackRepository>();
		resolveRepositoryPath = A.Fake<IResolveRepositoryPath>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => resolveRepositoryPath.Resolve(A<string[]>.Ignored)).Returns(Task.FromResult(resolvedPath));

		command = new UntrackRepositoryCommand(untrackRepository, resolveRepositoryPath, logger);
	}

	[Fact]
	public async Task ResolveRepositoryPathFromArguments()
	{
		var args = new[] { "untrack", "my-api" };

		await command.Execute(args);

		A.CallTo(() => resolveRepositoryPath.Resolve(args)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UntrackTheResolvedRepository()
	{
		await command.Execute(new[] { "untrack" });

		A.CallTo(() => untrackRepository.Untrack(resolvedPath)).MustHaveHappenedOnceExactly();
	}
}
