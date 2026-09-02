using System.Diagnostics.CodeAnalysis;
using Autofac;
using Serilog;

namespace FatCat.CodeWorker.Cli;

[ExcludeFromCodeCoverage]
public class CodeWorkerCliModule : Module
{
	protected override void Load(ContainerBuilder builder)
	{
		var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

		builder.RegisterInstance(logger).As<ILogger>().SingleInstance();
	}
}
