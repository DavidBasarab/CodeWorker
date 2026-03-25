using System.Diagnostics.CodeAnalysis;
using Autofac;
using FatCat.CodeWorker.Logging;
using Serilog;

namespace FatCat.CodeWorker;

[ExcludeFromCodeCoverage]
public class CodeWorkerModule : Module
{
	protected override void Load(ContainerBuilder builder)
	{
		var logger = SerilogConfiguration.Initialize();

		builder.RegisterInstance(logger).As<ILogger>().SingleInstance();
	}
}
