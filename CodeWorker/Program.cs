using System.Reflection;
using Autofac;
using FatCat.CodeWorker.Logging;
using FatCat.Toolkit.Console;
using FatCat.Toolkit.Injection;
using Serilog;

namespace FatCat.CodeWorker;

public static class Program
{
	public static async Task Main(params string[] args)
	{
		ConsoleLog.LogCallerInformation = true;

		TerminationDiagnostics.Install();

		try
		{
			SystemScope.Initialize(
				new ContainerBuilder(),
				[typeof(Program).Assembly, typeof(ConsoleLog).Assembly],
				ScopeOptions.SetLifetimeScope
			);

			var application = SystemScope.Container.Resolve<CodeWorkerApplication>();

			await application.DoWork(args);
		}
		catch (Exception ex)
		{
			ConsoleLog.WriteException(ex);
		}
		finally
		{
			Log.CloseAndFlush();
			Console.Out.Flush();
		}
	}
}
