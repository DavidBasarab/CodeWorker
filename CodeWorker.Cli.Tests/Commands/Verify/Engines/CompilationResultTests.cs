using FatCat.CodeWorker.Cli.Commands.Verify.Engines;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class CompilationResultTests
{
	private readonly CompilationResult result;

	public CompilationResultTests()
	{
		result = new CompilationResult();
	}

	[Fact]
	public void ReportNoErrorsWhenThereAreNoDiagnostics()
	{
		result.HasErrors.Should().BeFalse();
	}

	[Fact]
	public void ReportNoErrorsWhenOnlyWarningsArePresent()
	{
		result.Diagnostics =
		[
			CreateDiagnostic(CompilationDiagnosticSeverity.Warning),
			CreateDiagnostic(CompilationDiagnosticSeverity.Warning),
		];

		result.HasErrors.Should().BeFalse();
	}

	[Fact]
	public void ReportErrorsWhenAnyDiagnosticIsAnError()
	{
		result.Diagnostics =
		[
			CreateDiagnostic(CompilationDiagnosticSeverity.Warning),
			CreateDiagnostic(CompilationDiagnosticSeverity.Error),
		];

		result.HasErrors.Should().BeTrue();
	}

	[Fact]
	public void StartWithAnEmptyAssemblyImage()
	{
		result.AssemblyImage.Should().BeEmpty();
	}

	[Fact]
	public void StartWithNoDiagnostics()
	{
		result.Diagnostics.Should().BeEmpty();
	}

	private static CompilationDiagnostic CreateDiagnostic(CompilationDiagnosticSeverity severity)
	{
		var diagnostic = Faker.Create<CompilationDiagnostic>();

		diagnostic.Severity = severity;

		return diagnostic;
	}
}
