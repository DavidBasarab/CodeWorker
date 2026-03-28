namespace FatCat.CodeWorker.Settings;

public class GitSettings
{
	public bool CommitAfterEachTask { get; set; }

	public bool PushAfterEachTask { get; set; }

	public bool PullBeforeEachTask { get; set; }

	public string CommitMessagePrefix { get; set; }

	public string Branch { get; set; }
}
