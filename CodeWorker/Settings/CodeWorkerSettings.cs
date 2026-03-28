namespace FatCat.CodeWorker.Settings;

public class CodeWorkerSettings
{
	public List<RepositorySettings> Repositories { get; set; }

	public GitSettings Git { get; set; }

	public ClaudeSettings Claude { get; set; }
}
