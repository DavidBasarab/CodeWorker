using FatCat.CodeWorker.Settings;

namespace FatCat.CodeWorker.Commands.Run;

public interface IBuildTaskFolders
{
	TaskFolders Build(string repositoryPath, TaskSettings tasks);
}

public class BuildTaskFolders : IBuildTaskFolders
{
	public TaskFolders Build(string repositoryPath, TaskSettings tasks)
	{
		return new TaskFolders
		{
			Todo = Path.Combine(repositoryPath, tasks.TodoFolder),
			Pending = Path.Combine(repositoryPath, tasks.PendingFolder),
			Done = Path.Combine(repositoryPath, tasks.DoneFolder),
			Blocked = Path.Combine(repositoryPath, tasks.BlockedFolder),
			Failed = Path.Combine(repositoryPath, tasks.FailedFolder),
			Reference = Path.Combine(repositoryPath, tasks.ReferenceFolder),
		};
	}
}
