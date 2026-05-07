using System.Runtime.InteropServices;
using Serilog;

namespace FatCat.CodeWorker.Logging;

public static class TerminationDiagnostics
{
	public static void Install()
	{
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
		Console.CancelKeyPress += OnCancelKeyPress;

		RegisterPosixSignal(PosixSignal.SIGTERM);
		RegisterPosixSignal(PosixSignal.SIGINT);
		RegisterPosixSignal(PosixSignal.SIGHUP);
		RegisterPosixSignal(PosixSignal.SIGQUIT);
	}

	private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
	{
		var exception = args.ExceptionObject as Exception;

		Log.Fatal(
			exception,
			"UnhandledException IsTerminating={IsTerminating} ExceptionType={ExceptionType}",
			args.IsTerminating,
			exception?.GetType().FullName ?? "unknown"
		);

		Log.CloseAndFlush();
	}

	private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
	{
		Log.Error(args.Exception, "UnobservedTaskException Observed={Observed}", args.Observed);

		args.SetObserved();
	}

	private static void OnProcessExit(object sender, EventArgs args)
	{
		Log.Information("ProcessExit fired — host is terminating");

		Log.CloseAndFlush();
	}

	private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
	{
		Log.Warning("CancelKeyPress received SpecialKey={SpecialKey} Cancel={Cancel}", args.SpecialKey, args.Cancel);
	}

	private static void RegisterPosixSignal(PosixSignal signal)
	{
		try
		{
			PosixSignalRegistration.Create(
				signal,
				context =>
				{
					Log.Warning("PosixSignal received Signal={Signal}", context.Signal);
				}
			);
		}
		catch (PlatformNotSupportedException)
		{
			// ignored — not all signals are supported on every platform
		}
	}
}
