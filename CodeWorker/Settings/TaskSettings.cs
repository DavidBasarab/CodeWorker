namespace FatCat.CodeWorker.Settings;

public class TaskSettings
{
	public string TodoFolder { get; set; }

	public string DoneFolder { get; set; }

	public string ReferenceFolder { get; set; }

	public string PendingFolder { get; set; }

	public string BlockedFolder { get; set; }

	public bool StopOnBlocked { get; set; }

	public bool RunPlanningPhase { get; set; }
}
