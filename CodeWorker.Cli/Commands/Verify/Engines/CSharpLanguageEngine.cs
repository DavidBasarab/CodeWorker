namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class CSharpLanguageEngine : ILanguageEngine
{
	public string Language
	{
		get { return "C#"; }
	}

	public IReadOnlyList<string> FileExtensions
	{
		get { return [".cs"]; }
	}
}
