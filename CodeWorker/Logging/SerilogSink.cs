using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Serilog.Events;

namespace FatCat.CodeWorker.Logging;

[ExcludeFromCodeCoverage(Justification = "Logging infrastructure — no business logic")]
public abstract class SerilogSink(IFormatProvider formatProvider)
{
	protected readonly IFormatProvider formatProvider = formatProvider ?? new DateTimeFormatInfo();

	protected string GetFinalMessage(LogEvent logEvent)
	{
		var message = logEvent.RenderMessage(formatProvider);

		var logLevel = GetLogLevel(logEvent.Level);

		return $"{DateTime.Now:yyyy.MM.dd HH:mm:ss:fff} [{logLevel}] {message}";
	}

	protected string GetLogLevel(LogEventLevel logEventLevel)
	{
		return logEventLevel switch
		{
			LogEventLevel.Verbose => "VRB",
			LogEventLevel.Debug => "DBG",
			LogEventLevel.Information => "INF",
			LogEventLevel.Warning => "WRN",
			LogEventLevel.Error => "ERR",
			LogEventLevel.Fatal => "FTL",
			_ => throw new ArgumentOutOfRangeException(nameof(logEventLevel), logEventLevel, null),
		};
	}
}
