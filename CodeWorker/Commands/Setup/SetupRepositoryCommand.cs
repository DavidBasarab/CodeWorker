using FatCat.CodeWorker.FileSystem;
using Serilog;

namespace FatCat.CodeWorker.Commands.Setup;

public interface IRunSetupCommand : ICommand { }

public class SetupRepositoryCommand(ISetupRepository setupRepository, IGetWorkingDirectory getWorkingDirectory, ILogger logger)
	: IRunSetupCommand
{
	public async Task Execute(string[] args)
	{
		var repositoryPath = args.Length > 1 ? args[1] : getWorkingDirectory.GetWorkingDirectory();

		logger.Information("Running setup for repository at {RepositoryPath}", repositoryPath);

		await setupRepository.Setup(repositoryPath);
	}
}
