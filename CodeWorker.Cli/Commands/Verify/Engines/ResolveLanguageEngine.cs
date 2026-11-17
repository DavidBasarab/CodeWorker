namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public interface IResolveLanguageEngine
{
	LanguageEngineResult Resolve(string productionPath);
}

public class ResolveLanguageEngine(IEnumerable<ILanguageEngine> engines) : IResolveLanguageEngine
{
	public LanguageEngineResult Resolve(string productionPath)
	{
		var extension = Path.GetExtension(productionPath);
		var matchingEngines = EnginesHandling(extension);

		return matchingEngines.Count switch
		{
			0 => NoEngine(extension, productionPath),
			1 => Resolved(matchingEngines[0]),
			_ => MultipleEngines(extension, matchingEngines),
		};
	}

	private List<ILanguageEngine> EnginesHandling(string extension)
	{
		return engines
			.Where(engine =>
				engine.FileExtensions.Any(handled => string.Equals(handled, extension, StringComparison.OrdinalIgnoreCase))
			)
			.ToList();
	}

	private static LanguageEngineResult Resolved(ILanguageEngine engine)
	{
		return new LanguageEngineResult
		{
			IsResolved = true,
			Error = LanguageEngineError.None,
			Engine = engine,
		};
	}

	private static LanguageEngineResult NoEngine(string extension, string productionPath)
	{
		return Failure(
			LanguageEngineError.NoEngineForExtension,
			$"No language engine handles '{extension}' files: {productionPath}"
		);
	}

	private static LanguageEngineResult MultipleEngines(string extension, List<ILanguageEngine> matchingEngines)
	{
		var languages = matchingEngines.Select(engine => engine.Language);

		return Failure(
			LanguageEngineError.MultipleEnginesForExtension,
			$"More than one language engine handles '{extension}' files: {string.Join(", ", languages)}"
		);
	}

	private static LanguageEngineResult Failure(LanguageEngineError error, string message)
	{
		return new LanguageEngineResult
		{
			IsResolved = false,
			Error = error,
			Message = message,
		};
	}
}
