using Serilog;

namespace FatCat.CodeWorker.Logging;

public class FirstChanceExceptionLogger(ILogger logger)
{
	public void Log(Exception exception)
	{
		try
		{
			if (!ShouldLog(exception))
			{
				return;
			}

			logger.Debug(
				exception,
				"FirstChanceException Type={ExceptionType} Message={Message}",
				exception.GetType().FullName,
				exception.Message
			);
		}
		catch
		{
			// reentrancy guard — diagnostic-only handler must not propagate, see errors-and-logging.md
		}
	}

	private static bool ShouldLog(Exception exception)
	{
		return exception switch
		{
			OperationCanceledException => false,
			ThreadAbortException => false,
			_ when IsFileProviderProbe(exception) => false,
			_ => true,
		};
	}

	private static bool IsFileProviderProbe(Exception exception)
	{
		if (exception is not FileNotFoundException)
		{
			return false;
		}

		var stack = exception.StackTrace;

		return stack != null && stack.Contains("Microsoft.Extensions.FileProviders", StringComparison.Ordinal);
	}
}
