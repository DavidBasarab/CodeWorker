using System.Reflection;
using Autofac;
using FatCat.Toolkit.Console;
using FatCat.Toolkit.Injection;

namespace FatCat.CodeWorker;

public static class Program
{
	public static async Task Main(params string[] args)
	{
		await Task.CompletedTask;

		ConsoleLog.LogCallerInformation = true;

		try
		{
			SystemScope.Initialize(
				new ContainerBuilder(),
				new List<Assembly> { typeof(Program).Assembly, typeof(ConsoleLog).Assembly },
				ScopeOptions.SetLifetimeScope
			);

			var application = SystemScope.Container.Resolve<CodeWorkerApplication>();

			await application.DoWork(args);
		}
		catch (Exception ex)
		{
			ConsoleLog.WriteException(ex);
		}
	}
}
