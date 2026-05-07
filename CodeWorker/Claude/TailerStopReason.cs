namespace FatCat.CodeWorker.Claude;

public enum TailerStopReason
{
	OrchestratorDone,
	ResultEvent,
	WallClockTimeout,
	IdleTimeout,
}
