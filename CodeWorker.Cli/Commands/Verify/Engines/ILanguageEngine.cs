namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public interface ILanguageEngine
{
	string Language { get; }

	IReadOnlyList<string> FileExtensions { get; }
}
