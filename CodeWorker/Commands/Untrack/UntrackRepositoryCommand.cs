using Serilog;

namespace FatCat.CodeWorker.Commands.Untrack;

public interface IRunUntrackCommand : ICommand { }

public class UntrackRepositoryCommand(
	IUntrackRepository untrackRepository,
	IResolveRepositoryPath resolveRepositoryPath,
	ILogger logger
) : IRunUntrackCommand
{
	public async Task Execute(string[] args)
	{
		var repositoryPath = await resolveRepositoryPath.Resolve(args);

		logger.Information("Running untrack for repository at {RepositoryPath}", repositoryPath);

		await untrackRepository.Untrack(repositoryPath);
	}
}
