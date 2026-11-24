using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public interface ICompileInMemory
{
	CompilationResult Compile(string assemblyName, IReadOnlyList<SourceFile> sources, IReadOnlyList<string> referencePaths);
}

public class CompileInMemory : ICompileInMemory
{
	private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Default);

	private static readonly IReadOnlyList<string> OverlayCausedDiagnosticIds = ["CS0436", "CS1701", "CS1702"];

	public CompilationResult Compile(
		string assemblyName,
		IReadOnlyList<SourceFile> sources,
		IReadOnlyList<string> referencePaths
	)
	{
		var compilation = CSharpCompilation.Create(
			assemblyName,
			Parse(sources),
			References(referencePaths),
			CompilationOptions()
		);

		using var assemblyStream = new MemoryStream();

		var emitResult = compilation.Emit(assemblyStream);

		return new CompilationResult
		{
			Error = EngineError.None,
			Diagnostics = MapDiagnostics(emitResult.Diagnostics),
			AssemblyImage = emitResult.Success ? assemblyStream.ToArray() : [],
		};
	}

	private static IEnumerable<SyntaxTree> Parse(IReadOnlyList<SourceFile> sources)
	{
		return sources.Select(source => CSharpSyntaxTree.ParseText(source.Text, ParseOptions, source.FilePath));
	}

	private static IEnumerable<MetadataReference> References(IReadOnlyList<string> referencePaths)
	{
		return referencePaths.Select(path => MetadataReference.CreateFromFile(path));
	}

	private static CSharpCompilationOptions CompilationOptions()
	{
		return new CSharpCompilationOptions(
			OutputKind.DynamicallyLinkedLibrary,
			nullableContextOptions: NullableContextOptions.Disable,
			optimizationLevel: OptimizationLevel.Debug
		).WithSpecificDiagnosticOptions(OverlayCausedDiagnosticIds.ToDictionary(id => id, _ => ReportDiagnostic.Suppress));
	}

	private static List<CompilationDiagnostic> MapDiagnostics(ImmutableArray<Diagnostic> diagnostics)
	{
		return diagnostics.Where(IsReported).Select(MapDiagnostic).ToList();
	}

	private static bool IsReported(Diagnostic diagnostic)
	{
		return diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error;
	}

	private static CompilationDiagnostic MapDiagnostic(Diagnostic diagnostic)
	{
		var compilationDiagnostic = new CompilationDiagnostic
		{
			Id = diagnostic.Id,
			Message = diagnostic.GetMessage(),
			Severity = MapSeverity(diagnostic.Severity),
		};

		if (diagnostic.Location.IsInSource)
		{
			AddSourceLocation(compilationDiagnostic, diagnostic.Location.GetMappedLineSpan());
		}

		return compilationDiagnostic;
	}

	private static CompilationDiagnosticSeverity MapSeverity(DiagnosticSeverity severity)
	{
		return severity switch
		{
			DiagnosticSeverity.Warning => CompilationDiagnosticSeverity.Warning,
			DiagnosticSeverity.Error => CompilationDiagnosticSeverity.Error,
			_ => throw new ArgumentOutOfRangeException(nameof(severity)),
		};
	}

	private static void AddSourceLocation(CompilationDiagnostic compilationDiagnostic, FileLinePositionSpan span)
	{
		compilationDiagnostic.FilePath = span.Path;
		compilationDiagnostic.Line = span.StartLinePosition.Line + 1;
		compilationDiagnostic.Column = span.StartLinePosition.Character + 1;
	}
}
