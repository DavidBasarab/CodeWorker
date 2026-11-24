using FatCat.CodeWorker.Cli.Commands.Verify.Engines;
using FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class CSharpLanguageEngineTests
{
	private readonly string productionPath;
	private readonly string testsPath;
	private readonly IFindProjectBuild findProjectBuild;
	private readonly IGatherCompilationSources gatherCompilationSources;
	private readonly IGatherCompilationReferences gatherCompilationReferences;
	private readonly ICompileInMemory compileInMemory;
	private readonly List<SourceFile> gatheredSources;
	private readonly List<string> gatheredReferences;
	private readonly CompilationResult compilerResult;
	private readonly CSharpLanguageEngine engine;

	private ProjectBuildResult currentTestBuildResult;
	private ProjectBuildResult currentProductionBuildResult;

	public CSharpLanguageEngineTests()
	{
		findProjectBuild = A.Fake<IFindProjectBuild>();
		gatherCompilationSources = A.Fake<IGatherCompilationSources>();
		gatherCompilationReferences = A.Fake<IGatherCompilationReferences>();
		compileInMemory = A.Fake<ICompileInMemory>();

		productionPath = Faker.Create<string>();
		testsPath = Faker.Create<string>();
		currentTestBuildResult = SuccessfulBuildResult();
		currentProductionBuildResult = SuccessfulBuildResult();
		gatheredSources = Faker.Create<List<SourceFile>>();
		gatheredReferences = Faker.Create<List<string>>();
		compilerResult = Faker.Create<CompilationResult>();

		A.CallTo(() => findProjectBuild.Find(testsPath)).ReturnsLazily(() => currentTestBuildResult);
		A.CallTo(() => findProjectBuild.Find(productionPath)).ReturnsLazily(() => currentProductionBuildResult);
		A.CallTo(() => gatherCompilationSources.Gather(A<string>._, A<string>._, A<ProjectBuild>._, A<ProjectBuild>._))
			.Returns(gatheredSources);
		A.CallTo(() => gatherCompilationReferences.Gather(A<string>._)).Returns(gatheredReferences);
		A.CallTo(() => compileInMemory.Compile(A<string>._, A<IReadOnlyList<SourceFile>>._, A<IReadOnlyList<string>>._))
			.Returns(compilerResult);

		engine = new CSharpLanguageEngine(
			findProjectBuild,
			gatherCompilationSources,
			gatherCompilationReferences,
			compileInMemory
		);
	}

	private static ProjectBuildResult SuccessfulBuildResult()
	{
		return new ProjectBuildResult { Error = EngineError.None, Build = Faker.Create<ProjectBuild>() };
	}

	private static ProjectBuildResult FailedBuildResult(EngineError error)
	{
		return new ProjectBuildResult { Error = error, Message = Faker.Create<string>() };
	}

	private Task<CompilationResult> Compile()
	{
		return engine.Compile(productionPath, testsPath);
	}

	private void AssertNothingWasCompiled()
	{
		A.CallTo(() => compileInMemory.Compile(A<string>._, A<IReadOnlyList<SourceFile>>._, A<IReadOnlyList<string>>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void ReportCSharpAsItsLanguage()
	{
		engine.Language.Should().Be("C#");
	}

	[Fact]
	public void HandleTheCsExtension()
	{
		engine.FileExtensions.Should().Contain(".cs");
	}

	[Fact]
	public void HandleOnlyOneExtension()
	{
		engine.FileExtensions.Should().HaveCount(1);
	}

	[Fact]
	public async Task FindTheTestProjectBuild()
	{
		await Compile();

		A.CallTo(() => findProjectBuild.Find(testsPath)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FindTheProductionProjectBuild()
	{
		await Compile();

		A.CallTo(() => findProjectBuild.Find(productionPath)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnTheTestProjectsEngineErrorWhenItCannotBeFound()
	{
		currentTestBuildResult = FailedBuildResult(EngineError.ProjectNotFound);

		var result = await Compile();

		result.Error.Should().Be(EngineError.ProjectNotFound);
	}

	[Fact]
	public async Task ReturnTheTestProjectsErrorMessageWhenItCannotBeFound()
	{
		currentTestBuildResult = FailedBuildResult(EngineError.ProjectNotFound);

		var result = await Compile();

		result.Message.Should().Be(currentTestBuildResult.Message);
	}

	[Fact]
	public async Task NotCompileWhenTheTestProjectCannotBeFound()
	{
		currentTestBuildResult = FailedBuildResult(EngineError.ProjectNotFound);

		await Compile();

		AssertNothingWasCompiled();
	}

	[Fact]
	public async Task ReturnTheProductionProjectsEngineErrorWhenItCannotBeFound()
	{
		currentProductionBuildResult = FailedBuildResult(EngineError.MultipleProjectsFound);

		var result = await Compile();

		result.Error.Should().Be(EngineError.MultipleProjectsFound);
	}

	[Fact]
	public async Task ReturnTheProductionProjectsErrorMessageWhenItCannotBeFound()
	{
		currentProductionBuildResult = FailedBuildResult(EngineError.MultipleProjectsFound);

		var result = await Compile();

		result.Message.Should().Be(currentProductionBuildResult.Message);
	}

	[Fact]
	public async Task NotCompileWhenTheProductionProjectCannotBeFound()
	{
		currentProductionBuildResult = FailedBuildResult(EngineError.MultipleProjectsFound);

		await Compile();

		AssertNothingWasCompiled();
	}

	[Fact]
	public async Task GatherSourcesForBothFilesAndBothProjects()
	{
		await Compile();

		A.CallTo(() =>
				gatherCompilationSources.Gather(
					productionPath,
					testsPath,
					currentProductionBuildResult.Build,
					currentTestBuildResult.Build
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GatherReferencesFromTheTestProjectsBuildOutput()
	{
		await Compile();

		A.CallTo(() => gatherCompilationReferences.Gather(currentTestBuildResult.Build.OutputDirectory))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CompileTheGatheredSourcesAndReferences()
	{
		await Compile();

		A.CallTo(() => compileInMemory.Compile(A<string>._, gatheredSources, gatheredReferences)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NameTheAssemblyAfterTheTestAssemblyWithAVerifySuffix()
	{
		await Compile();

		A.CallTo(() =>
				compileInMemory.Compile(
					$"{currentTestBuildResult.Build.AssemblyName}.Verify",
					A<IReadOnlyList<SourceFile>>._,
					A<IReadOnlyList<string>>._
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnTheCompilersDiagnostics()
	{
		var result = await Compile();

		result.Diagnostics.Should().BeEquivalentTo(compilerResult.Diagnostics);
	}

	[Fact]
	public async Task SetTheBuildOutputDirectoryToTheTestProjectsOutput()
	{
		var result = await Compile();

		result.BuildOutputDirectory.Should().Be(currentTestBuildResult.Build.OutputDirectory);
	}
}
