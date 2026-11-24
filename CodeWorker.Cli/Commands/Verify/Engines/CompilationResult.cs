namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class CompilationResult
{
	public EngineError Error { get; set; }

	public string Message { get; set; }

	public List<CompilationDiagnostic> Diagnostics { get; set; } = [];

	public byte[] AssemblyImage { get; set; } = [];

	public string BuildOutputDirectory { get; set; }

	public bool HasErrors
	{
		get { return Diagnostics.Any(diagnostic => diagnostic.Severity == CompilationDiagnosticSeverity.Error); }
	}
}
