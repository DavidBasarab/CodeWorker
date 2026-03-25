using System.Diagnostics.CodeAnalysis;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;
using Serilog.Exceptions.Filters;

namespace FatCat.CodeWorker.Logging;

[ExcludeFromCodeCoverage(Justification = "Configuration class")]
public class DestructingOption : IDestructuringOptions
{
	public IEnumerable<IExceptionDestructurer> Destructurers
	{
		get { return DestructuringOptionsBuilder.DefaultDestructurers; }
	}

	public int DestructuringDepth
	{
		get { return 30; }
	}

	public bool DisableReflectionBasedDestructurer
	{
		get { return false; }
	}

	public IExceptionPropertyFilter Filter
	{
		get { return new IgnorePropertyByNameExceptionFilter(); }
	}

	public string RootName
	{
		get { return "ExceptionInfo"; }
	}
}
