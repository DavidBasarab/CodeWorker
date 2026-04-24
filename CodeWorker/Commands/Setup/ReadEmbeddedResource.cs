using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace FatCat.CodeWorker.Commands.Setup;

public interface IReadEmbeddedResource
{
	string Read(string resourceName);
}

[ExcludeFromCodeCoverage(Justification = "Direct wrapper over Assembly.GetManifestResourceStream — no business logic.")]
public class ReadEmbeddedResource : IReadEmbeddedResource
{
	public string Read(string resourceName)
	{
		var assembly = Assembly.GetExecutingAssembly();
		var fullResourceName = $"FatCat.CodeWorker.Commands.Setup.{resourceName}";

		using var stream = assembly.GetManifestResourceStream(fullResourceName);
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
