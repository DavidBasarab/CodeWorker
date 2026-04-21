using FatCat.CodeWorker.Commands;
using FatCat.CodeWorker.Commands.Track;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Track;

public class TrackRepositoryCommandTests
{
	private readonly ITrackRepository trackRepository;
	private readonly IResolveRepositoryPath resolveRepositoryPath;
	private readonly ILogger logger;
	private readonly TrackRepositoryCommand command;
	private readonly string resolvedPath = @"C:\Projects\my-api";

	public TrackRepositoryCommandTests()
	{
		trackRepository = A.Fake<ITrackRepository>();
		resolveRepositoryPath = A.Fake<IResolveRepositoryPath>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => resolveRepositoryPath.Resolve(A<string[]>.Ignored)).Returns(Task.FromResult(resolvedPath));

		command = new TrackRepositoryCommand(trackRepository, resolveRepositoryPath, logger);
	}

	[Fact]
	public async Task ResolveRepositoryPathFromArguments()
	{
		var args = new[] { "track", "my-api" };

		await command.Execute(args);

		A.CallTo(() => resolveRepositoryPath.Resolve(args)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task TrackTheResolvedRepository()
	{
		await command.Execute(new[] { "track" });

		A.CallTo(() => trackRepository.Track(resolvedPath)).MustHaveHappenedOnceExactly();
	}
}
