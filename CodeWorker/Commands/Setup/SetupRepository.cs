using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Commands.Setup;

public interface ISetupRepository
{
	Task Setup(string repositoryPath);
}

public class SetupRepository(IFileSystemTools fileSystemTools, ILogger logger) : ISetupRepository
{
	private const string ReadmeContent = """
		# Tasks

		This folder is managed by [CodeWorker](https://github.com/DavidBasarab/CodeWorker) — an overnight task runner for Claude Code.

		## Folder Structure

		| Folder | Purpose |
		|--------|---------|
		| `todo/` | Place task files here. They are executed in filename order. |
		| `done/` | Completed tasks are moved here automatically. |
		| `blocked/` | Tasks that could not be completed are moved here with an explanation. |

		## Task File Format

		Task files are plain Markdown. The filename prefix controls execution order:

		```
		01-refactor-auth-service.md
		02-add-unit-tests-auth.md
		03-update-api-docs.md
		```

		Each file contains a self-contained prompt describing exactly what Claude should do.
		""";

	public async Task Setup(string repositoryPath)
	{
		var tasksPath = Path.Combine(repositoryPath, "tasks");
		var todoPath = Path.Combine(tasksPath, "todo");
		var donePath = Path.Combine(tasksPath, "done");
		var blockedPath = Path.Combine(tasksPath, "blocked");

		logger.Information("Setting up task folders at {RepositoryPath}", repositoryPath);

		fileSystemTools.EnsureDirectory(todoPath);
		fileSystemTools.EnsureDirectory(donePath);
		fileSystemTools.EnsureDirectory(blockedPath);

		await fileSystemTools.WriteAllText(Path.Combine(todoPath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(donePath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(blockedPath, ".gitkeep"), string.Empty);
		await fileSystemTools.WriteAllText(Path.Combine(tasksPath, "README.md"), ReadmeContent);

		logger.Information("Repository setup complete at {RepositoryPath}", repositoryPath);
	}
}
