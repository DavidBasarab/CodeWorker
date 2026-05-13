using Serilog;

namespace FatCat.CodeWorker.Logging;

public class PowerEventLogger(ILogger logger, IFlushLogs flusher)
{
	public void Log(PowerEventMode mode)
	{
		logger.Warning("Power mode change {Mode}", mode);

		if (mode == PowerEventMode.Suspend)
		{
			flusher.Flush();
		}
	}
}
