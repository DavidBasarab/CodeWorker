using FatCat.CodeWorker.Logging;
using Serilog;

namespace Testing.FatCat.CodeWorker.Logging;

public class FirstChanceExceptionLoggerTests
{
	private readonly ILogger logger;
	private readonly FirstChanceExceptionLogger firstChanceLogger;

	public FirstChanceExceptionLoggerTests()
	{
		logger = A.Fake<ILogger>();

		firstChanceLogger = new FirstChanceExceptionLogger(logger);
	}

	[Fact]
	public void LogInvalidOperationExceptionAtDebugLevel()
	{
		var exception = new InvalidOperationException("boom");

		firstChanceLogger.Log(exception);

		A.CallTo(() => logger.Debug(exception, A<string>.That.Contains("FirstChanceException"), A<string>._, A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void LogIncludeExceptionObjectForStackTraceRendering()
	{
		Exception thrown;

		try
		{
			throw new InvalidOperationException("with stack");
		}
		catch (InvalidOperationException caught)
		{
			thrown = caught;
		}

		firstChanceLogger.Log(thrown);

		A.CallTo(() => logger.Debug(thrown, A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DoNotLogOperationCanceledException()
	{
		firstChanceLogger.Log(new OperationCanceledException());

		A.CallTo(logger).MustNotHaveHappened();
	}

	[Fact]
	public void DoNotLogTaskCanceledException()
	{
		firstChanceLogger.Log(new TaskCanceledException());

		A.CallTo(logger).MustNotHaveHappened();
	}

	[Fact]
	public void DoNotLogFileNotFoundFromFileProviderProbe()
	{
		var exception = CreateExceptionWithStackTrace(
			new FileNotFoundException("missing"),
			"   at Microsoft.Extensions.FileProviders.Physical.PhysicalFileProvider.GetFileInfo(String subpath)"
		);

		firstChanceLogger.Log(exception);

		A.CallTo(logger).MustNotHaveHappened();
	}

	[Fact]
	public void LogFileNotFoundFromUnrelatedStack()
	{
		var exception = CreateExceptionWithStackTrace(
			new FileNotFoundException("missing"),
			"   at SomeApp.Module.LoadConfig()"
		);

		firstChanceLogger.Log(exception);

		A.CallTo(() => logger.Debug(exception, A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void LogAtMostOneEntryPerCall()
	{
		firstChanceLogger.Log(new InvalidOperationException("once"));

		A.CallTo(logger).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SwallowExceptionsThrownByTheLoggerItself()
	{
		A.CallTo(logger).Throws(new InvalidOperationException("logger blew up"));

		var act = () => firstChanceLogger.Log(new InvalidOperationException("real"));

		act.Should().NotThrow();
	}

	private static T CreateExceptionWithStackTrace<T>(T exception, string stackTrace)
		where T : Exception
	{
		var field = typeof(Exception).GetField(
			"_stackTraceString",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
		);
		field?.SetValue(exception, stackTrace);

		return exception;
	}
}
