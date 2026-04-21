using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Commands.Run;

public class TaskExecution
{
	public string TaskFile { get; set; }

	public string TaskName { get; set; }

	public string PendingFilePath { get; set; }

	public ProcessResult Result { get; set; }
}
