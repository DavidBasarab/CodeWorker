using FatCat.CodeWorker.Cli.Commands.Verify.Engines;
using FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class ResolveLanguageEngineTests
{
	private const string CSharpFilePath = @"C:\work\Foo.cs";

	private readonly List<ILanguageEngine> engines = [];
	private readonly ResolveLanguageEngine resolveLanguageEngine;

	public ResolveLanguageEngineTests()
	{
		resolveLanguageEngine = new ResolveLanguageEngine(engines);
	}

	private ILanguageEngine AddEngine(params string[] fileExtensions)
	{
		var engine = A.Fake<ILanguageEngine>();

		A.CallTo(() => engine.Language).Returns(Faker.Create<string>());
		A.CallTo(() => engine.FileExtensions).Returns(fileExtensions);

		engines.Add(engine);

		return engine;
	}

	private ILanguageEngine AddCSharpEngine()
	{
		return AddEngine(".cs");
	}

	private ILanguageEngine AddTypeScriptEngine()
	{
		return AddEngine(".ts", ".tsx");
	}

	[Fact]
	public void ResolveTheEngineThatHandlesTheExtension()
	{
		AddTypeScriptEngine();
		var cSharpEngine = AddCSharpEngine();

		var result = resolveLanguageEngine.Resolve(CSharpFilePath);

		result.Engine.Should().Be(cSharpEngine);
	}

	[Fact]
	public void ReportResolvedWhenOneEngineMatches()
	{
		AddCSharpEngine();
		AddTypeScriptEngine();

		var result = resolveLanguageEngine.Resolve(CSharpFilePath);

		result.IsResolved.Should().BeTrue();
	}

	[Fact]
	public void ReportNoErrorWhenOneEngineMatches()
	{
		AddCSharpEngine();
		AddTypeScriptEngine();

		var result = resolveLanguageEngine.Resolve(CSharpFilePath);

		result.Error.Should().Be(LanguageEngineError.None);
	}

	[Fact]
	public void MatchTheExtensionIgnoringCase()
	{
		var cSharpEngine = AddCSharpEngine();

		var result = resolveLanguageEngine.Resolve(@"C:\work\Foo.CS");

		result.Engine.Should().Be(cSharpEngine);
	}

	[Fact]
	public void ChooseAmongSeveralEnginesByExtension()
	{
		AddCSharpEngine();
		var typeScriptEngine = AddTypeScriptEngine();

		var result = resolveLanguageEngine.Resolve(@"C:\work\Foo.tsx");

		result.Engine.Should().Be(typeScriptEngine);
	}

	[Fact]
	public void ReturnNoEngineForAnUnsupportedExtension()
	{
		AddCSharpEngine();
		AddTypeScriptEngine();

		var result = resolveLanguageEngine.Resolve(@"C:\work\Foo.py");

		result.Error.Should().Be(LanguageEngineError.NoEngineForExtension);
	}

	[Fact]
	public void ReportNotResolvedForAnUnsupportedExtension()
	{
		AddCSharpEngine();
		AddTypeScriptEngine();

		var result = resolveLanguageEngine.Resolve(@"C:\work\Foo.py");

		result.IsResolved.Should().BeFalse();
	}

	[Fact]
	public void NameTheExtensionAndFileWhenNoEngineMatches()
	{
		AddCSharpEngine();
		var productionPath = @"C:\work\Foo.py";

		var result = resolveLanguageEngine.Resolve(productionPath);

		result.Message.Should().Be($"No language engine handles '.py' files: {productionPath}");
	}

	[Fact]
	public void ReturnNoEngineWhenTheFileHasNoExtension()
	{
		AddCSharpEngine();
		AddTypeScriptEngine();

		var result = resolveLanguageEngine.Resolve(@"C:\work\Makefile");

		result.Error.Should().Be(LanguageEngineError.NoEngineForExtension);
	}

	[Fact]
	public void ReturnNoEngineWhenNoEnginesAreRegistered()
	{
		var result = resolveLanguageEngine.Resolve(CSharpFilePath);

		result.Error.Should().Be(LanguageEngineError.NoEngineForExtension);
	}

	[Fact]
	public void ReturnMultipleEnginesWhenTwoClaimTheExtension()
	{
		AddCSharpEngine();
		AddCSharpEngine();

		var result = resolveLanguageEngine.Resolve(CSharpFilePath);

		result.Error.Should().Be(LanguageEngineError.MultipleEnginesForExtension);
	}

	[Fact]
	public void NameTheCompetingLanguagesWhenSeveralMatch()
	{
		var firstEngine = AddCSharpEngine();
		var secondEngine = AddCSharpEngine();

		var result = resolveLanguageEngine.Resolve(CSharpFilePath);

		result
			.Message.Should()
			.Be($"More than one language engine handles '.cs' files: {firstEngine.Language}, {secondEngine.Language}");
	}

	[Fact]
	public void ResolveTheRealCSharpEngineForACsFile()
	{
		var realCSharpEngine = new CSharpLanguageEngine(
			A.Fake<IFindProjectBuild>(),
			A.Fake<IGatherCompilationSources>(),
			A.Fake<IGatherCompilationReferences>(),
			A.Fake<ICompileInMemory>()
		);
		var resolveWithRealEngine = new ResolveLanguageEngine([realCSharpEngine]);

		var result = resolveWithRealEngine.Resolve(CSharpFilePath);

		result.Engine.Language.Should().Be("C#");
	}
}
