using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IValidateRepository
{
	RepositoryValidationResult Validate(RepositorySettings repository);
}

public class ValidateRepository(IFileSystemTools fileSystemTools, ILogger logger) : IValidateRepository
{
	public RepositoryValidationResult Validate(RepositorySettings repository)
	{
		var errors = new List<string>();

		var repositoryPath = repository.Path;
		var tasksPath = Path.Combine(repositoryPath, "tasks");

		CheckDirectory(repositoryPath, errors);
		CheckDirectory(tasksPath, errors);

		foreach (var folder in RequiredTaskFolders.All)
		{
			CheckDirectory(Path.Combine(tasksPath, folder), errors);
		}

		CheckFile(Path.Combine(tasksPath, "settings.json"), errors);

		var result = new RepositoryValidationResult { IsValid = errors.Count == 0, Errors = errors };

		if (!result.IsValid)
		{
			logger.Warning(
				"Repository {RepositoryPath} failed validation: {Errors}",
				repositoryPath,
				string.Join("; ", errors)
			);
		}

		return result;
	}

	private void CheckDirectory(string path, List<string> errors)
	{
		if (!fileSystemTools.DirectoryExists(path))
		{
			errors.Add($"Directory not found: {path}");
		}
	}

	private void CheckFile(string path, List<string> errors)
	{
		if (!fileSystemTools.FileExists(path))
		{
			errors.Add($"File not found: {path}");
		}
	}
}
