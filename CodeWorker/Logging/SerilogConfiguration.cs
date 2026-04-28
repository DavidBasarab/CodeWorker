using System.Diagnostics.CodeAnalysis;
using FatCat.Toolkit;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;

namespace FatCat.CodeWorker.Logging;

[ExcludeFromCodeCoverage(Justification = "Configuration class")]
public static class SerilogConfiguration
{
	private static readonly ApplicationTools appTools = new();

	private static LoggingLevelSwitch LoggingLevelSwitch { get; } = new(LogSettings.LogLevel);

	public static ILogger Initialize()
	{
		var serviceName = appTools.ExecutableName;

		var logPath = Path.Combine(appTools.ExecutingDirectory, $@"Logs\{serviceName}.log");

		var config = new LoggerConfiguration()
			.Enrich.WithExceptionDetails(new DestructingOption())
			.Enrich.FromLogContext()
			.WriteTo.ColoredConsole()
			.WriteTo.File(
				logPath,
				fileSizeLimitBytes: 30000000,
				rollOnFileSizeLimit: true,
				retainedFileCountLimit: 100,
				formatProvider: new DateTimeLogFormatProvider(),
				flushToDiskInterval: TimeSpan.FromSeconds(1)
			);

		config.MinimumLevel.ControlledBy(LoggingLevelSwitch);

		config.MinimumLevel.Override("Microsoft", LogEventLevel.Warning);
		config.MinimumLevel.Override("System", LogEventLevel.Warning);

		var logger = config.CreateLogger();

		Log.Logger = logger;

		return logger;
	}
}
