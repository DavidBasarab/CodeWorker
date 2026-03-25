using System.Diagnostics.CodeAnalysis;
using FatCat.Toolkit.Console;
using Serilog.Core;
using Serilog.Events;

namespace FatCat.CodeWorker.Logging;

[ExcludeFromCodeCoverage(Justification = "Logging infrastructure — no business logic")]
public class SerilogConsoleSink(IFormatProvider formatProvider) : SerilogSink(formatProvider), ILogEventSink
{
	public void Emit(LogEvent logEvent)
	{
		var finalMessage = GetFinalMessage(logEvent);

		var previousLogCallerSetting = ConsoleLog.LogCallerInformation;

		ConsoleLog.LogCallerInformation = false;

		try
		{
			if (logEvent.Exception != null)
			{
				ConsoleLog.WriteException(logEvent.Exception);

				return;
			}

			switch (logEvent.Level)
			{
				case LogEventLevel.Verbose:
					ConsoleLog.WriteDarkCyan(finalMessage);
					break;
				case LogEventLevel.Debug:
					Console.WriteLine(finalMessage);
					break;
				case LogEventLevel.Information:
					ConsoleLog.WriteGreen(finalMessage);
					break;
				case LogEventLevel.Warning:
					ConsoleLog.WriteYellow(finalMessage);
					break;
				case LogEventLevel.Error:
					ConsoleLog.WriteRed(finalMessage);
					break;
				case LogEventLevel.Fatal:
					ConsoleLog.WriteDarkRed(finalMessage);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
		finally
		{
			ConsoleLog.LogCallerInformation = previousLogCallerSetting;
		}
	}
}
