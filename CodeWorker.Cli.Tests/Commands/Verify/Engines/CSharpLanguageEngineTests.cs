using FatCat.CodeWorker.Cli.Commands.Verify.Engines;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines;

public class CSharpLanguageEngineTests
{
	private readonly CSharpLanguageEngine engine;

	public CSharpLanguageEngineTests()
	{
		engine = new CSharpLanguageEngine();
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
}
