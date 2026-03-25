using Serilog.Events;

namespace FatCat.CodeWorker.Logging;

public static class LogSettings
{
	public static LogEventLevel LogLevel
	{
		get { return LogEventLevel.Information; }
	}
}
