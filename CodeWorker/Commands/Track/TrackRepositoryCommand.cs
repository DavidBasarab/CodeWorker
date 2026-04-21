using Serilog;

namespace FatCat.CodeWorker.Commands.Track;

public interface IRunTrackCommand : ICommand { }

public class TrackRepositoryCommand(
	ITrackRepository trackRepository,
	IResolveRepositoryPath resolveRepositoryPath,
	ILogger logger
) : IRunTrackCommand
{
	public async Task Execute(string[] args)
	{
		var repositoryPath = await resolveRepositoryPath.Resolve(args);

		logger.Information("Running track for repository at {RepositoryPath}", repositoryPath);

		await trackRepository.Track(repositoryPath);
	}
}
