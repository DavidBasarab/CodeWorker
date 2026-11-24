using System.Diagnostics.CodeAnalysis;
using System.Reflection.PortableExecutable;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public interface IInspectAssemblyFile
{
	bool IsManagedAssembly(string path);
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over System.Reflection.PortableExecutable.PEReader and a FileStream — no business logic, tested via IInspectAssemblyFile fakes in GatherCompilationReferences."
)]
public class InspectAssemblyFile : IInspectAssemblyFile
{
	public bool IsManagedAssembly(string path)
	{
		try
		{
			using var stream = File.OpenRead(path);
			using var peReader = new PEReader(stream);

			return peReader.HasMetadata;
		}
		catch (BadImageFormatException)
		{
			return false;
		}
	}
}
