using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace FatCat.CodeWorker.Claude;

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over Assembly.GetManifestResourceStream and File.WriteAllText — no business logic."
)]
public class ExtractWrapperScript : IExtractWrapperScript
{
	private const string ResourceName = "FatCat.CodeWorker.Claude.Scripts.Run-ClaudeTask.ps1";

	private static readonly object syncRoot = new();
	private static string cachedPath;

	public string Extract()
	{
		lock (syncRoot)
		{
			if (cachedPath != null && File.Exists(cachedPath))
			{
				return cachedPath;
			}

			var directory = Path.Combine(Path.GetTempPath(), "CodeWorker", "Scripts");

			Directory.CreateDirectory(directory);

			var path = Path.Combine(directory, "Run-ClaudeTask.ps1");

			using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
			using var reader = new StreamReader(stream);

			var content = reader.ReadToEnd();

			File.WriteAllText(path, content);

			cachedPath = path;

			return cachedPath;
		}
	}
}
