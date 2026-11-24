using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public interface IReadTrustedPlatformAssemblies
{
	string RuntimeDirectory { get; }

	List<string> Read();
}

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over AppContext TRUSTED_PLATFORM_ASSEMBLIES and RuntimeEnvironment.GetRuntimeDirectory — no business logic, tested via IReadTrustedPlatformAssemblies fakes in GatherCompilationReferences."
)]
public class ReadTrustedPlatformAssemblies : IReadTrustedPlatformAssemblies
{
	public string RuntimeDirectory
	{
		get { return RuntimeEnvironment.GetRuntimeDirectory(); }
	}

	public List<string> Read()
	{
		var trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");

		return [.. trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)];
	}
}
