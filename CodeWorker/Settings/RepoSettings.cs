namespace FatCat.CodeWorker.Settings;

public class RepoSettings
{
	public bool Enabled { get; set; }

	public bool LogResults { get; set; }

	public GitSettings Git { get; set; }

	public ClaudeSettings Claude { get; set; }

	public TaskSettings Tasks { get; set; }
}
