namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class CompilationDiagnostic
{
	public string Id { get; set; }

	public string Message { get; set; }

	public CompilationDiagnosticSeverity Severity { get; set; }

	public string FilePath { get; set; }

	public int? Line { get; set; }

	public int? Column { get; set; }
}
