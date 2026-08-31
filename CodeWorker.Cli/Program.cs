using Autofac;
using FatCat.Toolkit.Console;
using FatCat.Toolkit.Injection;
using Serilog;

namespace FatCat.CodeWorker.Cli;

public static class Program
{
	public static async Task Main(params string[] args)
	{
		ConsoleLog.LogCallerInformation = true;

		try
		{
			SystemScope.Initialize(
				new ContainerBuilder(),
				[typeof(Program).Assembly, typeof(ConsoleLog).Assembly],
				ScopeOptions.SetLifetimeScope
			);

			var application = SystemScope.Container.Resolve<CodeWorkerCliApplication>();

			await application.Run(args);
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
