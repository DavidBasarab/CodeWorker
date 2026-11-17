using System.Diagnostics.CodeAnalysis;

namespace FatCat.CodeWorker.Cli.Time;

public interface IClock
{
	DateTime UtcNow { get; }
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over DateTime.UtcNow — no business logic, tested via IClock fakes in consuming classes."
)]
public class SystemClock : IClock
{
	public DateTime UtcNow
	{
		get { return DateTime.UtcNow; }
	}
}
