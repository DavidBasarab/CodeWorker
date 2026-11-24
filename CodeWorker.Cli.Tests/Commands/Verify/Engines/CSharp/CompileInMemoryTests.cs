using System.Runtime.InteropServices;
using FatCat.CodeWorker.Cli.Commands.Verify.Engines;
using FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;
using FatCat.CodeWorker.Cli.Time;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public class CompileInMemoryTests
{
	private const int UndefinedNameLine = 5;
	private const int UndefinedNameColumn = 33;

	private static readonly string CleanSource = Lines(
		"namespace Sample;",
		"",
		"public class Greeter",
		"{",
		"    public string Greet() { return \"hello\"; }",
		"}"
	);

	private static readonly string UndefinedNameSource = Lines(
		"namespace Sample;",
		"",
		"public class Broken",
		"{",
		"    public int Value() { return undefinedName; }",
		"}"
	);

	private readonly string assemblyName;
	private readonly CompileInMemory compileInMemory;

	public CompileInMemoryTests()
	{
		assemblyName = Faker.Create<string>();

		compileInMemory = new CompileInMemory();
	}

	[Fact]
	public void ReturnNoErrorEngineResult()
	{
		var result = Compile(CleanSource);

		result.Error.Should().Be(EngineError.None);
	}

	[Fact]
	public void ReportNoDiagnosticsForCleanSource()
	{
		var result = Compile(CleanSource);

		result.Diagnostics.Should().BeEmpty();
	}

	[Fact]
	public void EmitAnAssemblyImageForCleanSource()
	{
		var result = Compile(CleanSource);

		result.AssemblyImage.Should().Not.BeEmpty();
	}

	[Fact]
	public void ReportAnUndefinedNameAsAnError()
	{
		var result = Compile(UndefinedNameSource);

		result.Diagnostics.Single().Id.Should().Be("CS0103");
	}

	[Fact]
	public void ReportTheErrorSeverity()
	{
		var result = Compile(UndefinedNameSource);

		result.Diagnostics.Single().Severity.Should().Be(CompilationDiagnosticSeverity.Error);
	}

	[Fact]
	public void ReportTheErrorsFilePath()
	{
		var source = CreateSource(UndefinedNameSource);

		var result = Compile(source);

		result.Diagnostics.Single().FilePath.Should().Be(source.FilePath);
	}

	[Fact]
	public void ReportTheErrorsOneBasedLine()
	{
		var result = Compile(UndefinedNameSource);

		result.Diagnostics.Single().Line.Should().Be(UndefinedNameLine);
	}

	[Fact]
	public void ReportTheErrorsOneBasedColumn()
	{
		var result = Compile(UndefinedNameSource);

		result.Diagnostics.Single().Column.Should().Be(UndefinedNameColumn);
	}

	[Fact]
	public void ReportTheErrorMessage()
	{
		var result = Compile(UndefinedNameSource);

		result.Diagnostics.Single().Message.Should().Not.BeNullOrEmpty();
	}

	[Fact]
	public void EmitNoAssemblyImageWhenThereAreErrors()
	{
		var result = Compile(UndefinedNameSource);

		result.AssemblyImage.Should().BeEmpty();
	}

	[Fact]
	public void ReportAWarningWithWarningSeverity()
	{
		var result = Compile(
			Lines("namespace Sample;", "", "public class Declares", "{", "    public void Run() { int unused; }", "}")
		);

		result
			.Diagnostics.Single(diagnostic => diagnostic.Id == "CS0168")
			.Severity.Should()
			.Be(CompilationDiagnosticSeverity.Warning);
	}

	[Fact]
	public void NotReportHiddenOrInfoDiagnostics()
	{
		var result = Compile(Lines("using System.Text;", "", "namespace Sample;", "", "public class Plain { }"));

		result.Diagnostics.Should().BeEmpty();
	}

	[Fact]
	public void NotReportDuplicateGlobalUsingsAcrossSources()
	{
		var result = Compile(
			CreateSource(Lines("global using System;", "", "namespace Sample;", "", "public class First { }")),
			CreateSource(Lines("global using System;", "", "namespace Sample;", "", "public class Second { }")),
			CreateSource(Lines("using System;", "", "namespace Sample;", "", "public class Third { public DateTime Stamp; }"))
		);

		result.Diagnostics.Should().BeEmpty();
	}

	[Fact]
	public void NotReportASourceTypeShadowingAReferencedType()
	{
		var shadowingSource = CreateSource(
			Lines(
				"namespace FatCat.CodeWorker.Cli.Time;",
				"",
				"public class SystemClock",
				"{",
				"    public System.DateTime UtcNow { get { return System.DateTime.MinValue; } }",
				"}"
			)
		);
		var consumingSource = CreateSource(
			Lines(
				"namespace Sample;",
				"",
				"public class ReadsTheClock",
				"{",
				"    public System.DateTime Now() { return new FatCat.CodeWorker.Cli.Time.SystemClock().UtcNow; }",
				"}"
			)
		);
		var references = FrameworkReferences().Append(typeof(SystemClock).Assembly.Location).ToList();

		var result = compileInMemory.Compile(assemblyName, [shadowingSource, consumingSource], references);

		result.Diagnostics.Should().BeEmpty();
	}

	[Fact]
	public void CompileWithNullableDisabled()
	{
		var result = Compile(Lines("namespace Sample;", "", "public class Annotated", "{", "    public string? Value;", "}"));

		result.Diagnostics.Single().Id.Should().Be("CS8632");
	}

	[Fact]
	public void CompileFileScopedNamespacesAndPrimaryConstructors()
	{
		var result = Compile(
			Lines(
				"namespace Sample;",
				"",
				"public class Greeter(string name)",
				"{",
				"    public string Greet() { return $\"Hello {name}\"; }",
				"}"
			)
		);

		result.Diagnostics.Should().BeEmpty();
	}

	[Fact]
	public void ReportNoFilePathForADiagnosticOutsideTheSource()
	{
		var result = CompileWithoutReferences();

		result.Diagnostics.Single(diagnostic => diagnostic.Id == "CS8021").FilePath.Should().BeNull();
	}

	[Fact]
	public void ReportNoLineForADiagnosticOutsideTheSource()
	{
		var result = CompileWithoutReferences();

		result.Diagnostics.Single(diagnostic => diagnostic.Id == "CS8021").Line.Should().BeNull();
	}

	[Fact]
	public void ReportNoColumnForADiagnosticOutsideTheSource()
	{
		var result = CompileWithoutReferences();

		result.Diagnostics.Single(diagnostic => diagnostic.Id == "CS8021").Column.Should().BeNull();
	}

	private CompilationResult Compile(string text)
	{
		return Compile(CreateSource(text));
	}

	private CompilationResult Compile(params SourceFile[] sources)
	{
		return compileInMemory.Compile(assemblyName, sources, FrameworkReferences());
	}

	private CompilationResult CompileWithoutReferences()
	{
		return compileInMemory.Compile(assemblyName, [CreateSource(CleanSource)], []);
	}

	private static SourceFile CreateSource(string text)
	{
		return new SourceFile { FilePath = $"{Faker.Create<string>()}.cs", Text = text };
	}

	private static string Lines(params string[] lines)
	{
		return string.Join("\n", lines);
	}

	private static List<string> FrameworkReferences()
	{
		var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
		var trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");

		return trustedPlatformAssemblies
			.Split(Path.PathSeparator)
			.Where(path => path.StartsWith(runtimeDirectory, StringComparison.OrdinalIgnoreCase))
			.ToList();
	}
}
