using System.Diagnostics.CodeAnalysis;

namespace FatCat.CodeWorker.Cli.Process;

public interface ISetExitCode
{
	void Set(CliExitCode exitCode);
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over Environment.ExitCode — no business logic, tested via ISetExitCode fakes in consuming classes."
)]
public class SetExitCode : ISetExitCode
{
	public void Set(CliExitCode exitCode)
	{
		Environment.ExitCode = (int)exitCode;
	}
}
