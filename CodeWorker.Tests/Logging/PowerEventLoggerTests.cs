using FatCat.CodeWorker.Logging;
using Serilog;

namespace Testing.FatCat.CodeWorker.Logging;

public class PowerEventLoggerTests
{
	private readonly ILogger logger;
	private readonly IFlushLogs flusher;
	private readonly PowerEventLogger powerEventLogger;

	public PowerEventLoggerTests()
	{
		logger = A.Fake<ILogger>();
		flusher = A.Fake<IFlushLogs>();

		powerEventLogger = new PowerEventLogger(logger, flusher);
	}

	[Fact]
	public void LogSuspendAtWarningLevel()
	{
		powerEventLogger.Log(PowerEventMode.Suspend);

		A.CallTo(() => logger.Warning(A<string>.That.Contains("Power mode change"), PowerEventMode.Suspend))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void LogResumeAtWarningLevel()
	{
		powerEventLogger.Log(PowerEventMode.Resume);

		A.CallTo(() => logger.Warning(A<string>.That.Contains("Power mode change"), PowerEventMode.Resume))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void LogStatusChangeAtWarningLevel()
	{
		powerEventLogger.Log(PowerEventMode.StatusChange);

		A.CallTo(() => logger.Warning(A<string>.That.Contains("Power mode change"), PowerEventMode.StatusChange))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void FlushLogsOnSuspend()
	{
		powerEventLogger.Log(PowerEventMode.Suspend);

		A.CallTo(() => flusher.Flush()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DoNotFlushOnResume()
	{
		powerEventLogger.Log(PowerEventMode.Resume);

		A.CallTo(() => flusher.Flush()).MustNotHaveHappened();
	}

	[Fact]
	public void DoNotFlushOnStatusChange()
	{
		powerEventLogger.Log(PowerEventMode.StatusChange);

		A.CallTo(() => flusher.Flush()).MustNotHaveHappened();
	}
}
