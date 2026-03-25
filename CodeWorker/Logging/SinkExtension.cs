using System.Diagnostics.CodeAnalysis;
using Serilog;
using Serilog.Configuration;

namespace FatCat.CodeWorker.Logging;

[ExcludeFromCodeCoverage(Justification = "Logging infrastructure — no business logic")]
public static class SinkExtension
{
	public static LoggerConfiguration ColoredConsole(
		this LoggerSinkConfiguration loggerConfiguration,
		IFormatProvider formatProvider = null
	)
	{
		return loggerConfiguration.Sink(new SerilogConsoleSink(formatProvider));
	}
}
