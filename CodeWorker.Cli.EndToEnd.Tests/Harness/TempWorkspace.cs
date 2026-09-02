using System.Diagnostics.CodeAnalysis;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[ExcludeFromCodeCoverage(
	Justification = "Thin System.IO temp-file wrapper — no business logic, exercised by the E2E tests that use it."
)]
public class TempWorkspace : IDisposable
{
	private readonly string root;

	public TempWorkspace()
	{
		root = Path.Combine(Path.GetTempPath(), $"codeworker-cli-e2e-work-{Guid.NewGuid():N}");

		Directory.CreateDirectory(root);

		IntentPath = WriteFile("intent.json", "{ \"class\": \"Foo\" }");
		ProductionPath = WriteFile("Foo.cs", "public class Foo { }");
		TestsPath = WriteFile("FooTests.cs", "public class FooTests { }");
	}

	public string IntentPath { get; }

	public string ProductionPath { get; }

	public string TestsPath { get; }

	public void Dispose()
	{
		try
		{
			Directory.Delete(root, true);
		}
		catch
		{
			// ignored — best-effort cleanup of a temp directory
		}
	}

	private string WriteFile(string name, string content)
	{
		var path = Path.Combine(root, name);

		File.WriteAllText(path, content);

		return path;
	}
}
