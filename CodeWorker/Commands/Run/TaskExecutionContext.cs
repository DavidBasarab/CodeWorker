using FatCat.CodeWorker.Settings;

namespace FatCat.CodeWorker.Commands.Run;

public class TaskExecutionContext
{
	public RepositorySettings Repository { get; set; }

	public RepoSettings RepoSettings { get; set; }

	public ClaudeSettings ClaudeSettings { get; set; }

	public TaskFolders Folders { get; set; }

	public List<ReferenceFile> ReferenceFiles { get; set; }
}
