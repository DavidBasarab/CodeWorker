using FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;
using FatCat.Toolkit;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public class GatherCompilationReferencesTests
{
	private const string OutputDirectory = @"C:\work\Foo\bin\Debug\net11.0";
	private const string RuntimeDirectory = @"C:\dotnet\shared\Microsoft.NETCore.App\11.0.0\";

	private readonly IFileSystemTools fileSystemTools;
	private readonly IReadTrustedPlatformAssemblies readTrustedPlatformAssemblies;
	private readonly IInspectAssemblyFile inspectAssemblyFile;
	private readonly GatherCompilationReferences gatherCompilationReferences;

	private List<string> outputFiles = [];
	private List<string> trustedPlatformAssemblies = [];

	public GatherCompilationReferencesTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		readTrustedPlatformAssemblies = A.Fake<IReadTrustedPlatformAssemblies>();
		inspectAssemblyFile = A.Fake<IInspectAssemblyFile>();

		A.CallTo(() => fileSystemTools.GetFiles(OutputDirectory)).ReturnsLazily(() => outputFiles);
		A.CallTo(() => readTrustedPlatformAssemblies.Read()).ReturnsLazily(() => trustedPlatformAssemblies);
		A.CallTo(() => readTrustedPlatformAssemblies.RuntimeDirectory).Returns(RuntimeDirectory);
		A.CallTo(() => inspectAssemblyFile.IsManagedAssembly(A<string>._)).Returns(true);

		gatherCompilationReferences = new GatherCompilationReferences(
			fileSystemTools,
			readTrustedPlatformAssemblies,
			inspectAssemblyFile
		);
	}

	private static string OutputPath(string fileName)
	{
		return $@"{OutputDirectory}\{fileName}";
	}

	private static string FrameworkPath(string fileName)
	{
		return $"{RuntimeDirectory}{fileName}";
	}

	private void MarkAsNative(string path)
	{
		A.CallTo(() => inspectAssemblyFile.IsManagedAssembly(path)).Returns(false);
	}

	[Fact]
	public void IncludeManagedAssembliesFromTheBuildOutput()
	{
		var managedAssembly = OutputPath("Foo.dll");

		outputFiles = [managedAssembly];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([managedAssembly]);
	}

	[Fact]
	public void ExcludeNativeDllsFromTheBuildOutput()
	{
		var managedAssembly = OutputPath("Foo.dll");
		var nativeLibrary = OutputPath("e_sqlite3.dll");

		outputFiles = [managedAssembly, nativeLibrary];
		MarkAsNative(nativeLibrary);

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([managedAssembly]);
	}

	[Fact]
	public void ExcludeFilesThatAreNotDlls()
	{
		var managedAssembly = OutputPath("Foo.dll");

		outputFiles = [managedAssembly, OutputPath("Foo.pdb"), OutputPath("Foo.deps.json"), OutputPath("Foo.exe")];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([managedAssembly]);
	}

	[Fact]
	public void IncludeFrameworkAssembliesFromTheRuntimeDirectory()
	{
		var frameworkAssembly = FrameworkPath("System.Runtime.dll");

		trustedPlatformAssemblies = [frameworkAssembly];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([frameworkAssembly]);
	}

	[Fact]
	public void ExcludeTrustedAssembliesOutsideTheRuntimeDirectory()
	{
		var frameworkAssembly = FrameworkPath("System.Runtime.dll");

		trustedPlatformAssemblies = [frameworkAssembly, @"C:\tools\CodeWorkerCli\Serilog.dll"];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([frameworkAssembly]);
	}

	[Fact]
	public void MatchTheRuntimeDirectoryWithoutRegardToCase()
	{
		var frameworkAssembly = FrameworkPath("System.Runtime.dll").ToUpperInvariant();

		trustedPlatformAssemblies = [frameworkAssembly];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([frameworkAssembly]);
	}

	[Fact]
	public void PreferTheBuildOutputCopyOverTheFrameworkCopy()
	{
		var buildOutputCopy = OutputPath("System.Text.Json.dll");
		var frameworkRuntime = FrameworkPath("System.Runtime.dll");

		outputFiles = [buildOutputCopy];
		trustedPlatformAssemblies = [FrameworkPath("System.Text.Json.dll"), frameworkRuntime];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([buildOutputCopy, frameworkRuntime]);
	}

	[Fact]
	public void MatchFileNamesWithoutRegardToCase()
	{
		var buildOutputCopy = OutputPath("system.text.json.DLL");
		var frameworkRuntime = FrameworkPath("System.Runtime.dll");

		outputFiles = [buildOutputCopy];
		trustedPlatformAssemblies = [FrameworkPath("System.Text.Json.dll"), frameworkRuntime];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([buildOutputCopy, frameworkRuntime]);
	}

	[Fact]
	public void KeepTheFrameworkCopyWhenTheBuildOutputCopyIsNative()
	{
		var nativeLibrary = OutputPath("System.Text.Json.dll");
		var frameworkAssembly = FrameworkPath("System.Text.Json.dll");

		outputFiles = [nativeLibrary];
		trustedPlatformAssemblies = [frameworkAssembly];
		MarkAsNative(nativeLibrary);

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([frameworkAssembly]);
	}

	[Fact]
	public void ListBuildOutputAssembliesBeforeFrameworkAssemblies()
	{
		var frameworkAssembly = FrameworkPath("System.Runtime.dll");
		var managedAssembly = OutputPath("Foo.dll");

		trustedPlatformAssemblies = [frameworkAssembly];
		outputFiles = [managedAssembly];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([managedAssembly, frameworkAssembly]);
	}

	[Fact]
	public void ReturnOnlyFrameworkAssembliesWhenTheBuildOutputIsEmpty()
	{
		var frameworkRuntime = FrameworkPath("System.Runtime.dll");
		var frameworkCollections = FrameworkPath("System.Collections.dll");

		trustedPlatformAssemblies = [frameworkRuntime, frameworkCollections];

		var references = gatherCompilationReferences.Gather(OutputDirectory);

		references.Should().Equal([frameworkRuntime, frameworkCollections]);
	}
}
