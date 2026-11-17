namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class LanguageEngineResult
{
	public bool IsResolved { get; set; }

	public LanguageEngineError Error { get; set; }

	public string Message { get; set; }

	public ILanguageEngine Engine { get; set; }
}
