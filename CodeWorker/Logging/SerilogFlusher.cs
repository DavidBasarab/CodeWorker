using System.Diagnostics.CodeAnalysis;
using Serilog;

namespace FatCat.CodeWorker.Logging;

public interface IFlushLogs
{
	void Flush();
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over Serilog static Log.CloseAndFlush — no business logic, tested via IFlushLogs fakes in consuming classes."
)]
public class SerilogFlusher : IFlushLogs
{
	public void Flush()
	{
		Log.CloseAndFlush();
	}
}
