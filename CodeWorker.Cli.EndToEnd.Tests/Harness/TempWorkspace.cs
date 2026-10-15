using System.Diagnostics.CodeAnalysis;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[ExcludeFromCodeCoverage(
	Justification = "Thin System.IO temp-file wrapper — no business logic, exercised by the E2E tests that use it."
)]
public class TempWorkspace : IDisposable
{
	private const string ValidIntent = """
		{
		  "why": "End-to-end fixture for the verify command.",
		  "class": "Foo",
		  "responsibility": "Exist so verify has a production class to check.",
		  "behaviors": [ { "behavior": "Returns nothing when constructed", "test": "ReturnNothingWhenConstructed" } ]
		}
		""";

	private readonly string root;

	public TempWorkspace()
	{
		root = Path.Combine(Path.GetTempPath(), $"codeworker-cli-e2e-work-{Guid.NewGuid():N}");

		Directory.CreateDirectory(root);

		IntentPath = WriteFile("intent.json", ValidIntent);
		ProductionPath = WriteFile("Foo.cs", "public class Foo { }");
		TestsPath = WriteFile("FooTests.cs", "public class FooTests { }");
	}

	public string IntentPath { get; }

	public string ProductionPath { get; }

	public string TestsPath { get; }

	public void WriteIntent(string content)
	{
		WriteFile("intent.json", content);
	}

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
