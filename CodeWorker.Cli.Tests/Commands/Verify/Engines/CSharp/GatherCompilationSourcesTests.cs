using FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;
using FatCat.Toolkit;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public class GatherCompilationSourcesTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly GatherCompilationSources gatherCompilationSources;
	private readonly string productionPath;
	private readonly string testsPath;
	private readonly ProjectBuild productionBuild;
	private readonly ProjectBuild testBuild;

	public GatherCompilationSourcesTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();

		productionBuild = CreateProjectBuild();
		testBuild = CreateProjectBuild();
		productionPath = Path.Combine(productionBuild.ProjectDirectory, $"{Faker.Create<string>()}.cs");
		testsPath = Path.Combine(testBuild.ProjectDirectory, $"{Faker.Create<string>()}Tests.cs");

		A.CallTo(() => fileSystemTools.ReadAllText(A<string>._)).ReturnsLazily((string path) => $"text of {path}");

		gatherCompilationSources = new GatherCompilationSources(fileSystemTools);
	}

	[Fact]
	public async Task ReturnTheProductionFileFirst()
	{
		var sources = await Gather();

		sources[0].FilePath.Should().Be(productionPath);
	}

	[Fact]
	public async Task ReturnTheTestFileSecond()
	{
		var sources = await Gather();

		sources[1].FilePath.Should().Be(testsPath);
	}

	[Fact]
	public async Task ReturnOnlyTheSubmittedFilesWhenNoGlobalUsingsFilesExist()
	{
		var sources = await Gather();

		sources.Select(source => source.FilePath).Should().Equal([productionPath, testsPath]);
	}

	[Fact]
	public async Task IncludeTheTestProjectsGlobalUsingsFile()
	{
		var globalUsingsPath = AddGlobalUsingsFile(testBuild);

		var sources = await Gather();

		sources[2].FilePath.Should().Be(globalUsingsPath);
	}

	[Fact]
	public async Task IncludeTheTestProjectsGeneratedImplicitUsingsFile()
	{
		var generatedUsingsPath = AddGeneratedUsingsFile(testBuild);

		var sources = await Gather();

		sources[2].FilePath.Should().Be(generatedUsingsPath);
	}

	[Fact]
	public async Task NameTheGeneratedFileAfterTheProjectFile()
	{
		var projectName = Faker.Create<string>();
		testBuild.ProjectFilePath = Path.Combine(testBuild.ProjectDirectory, $"{projectName}.csproj");
		var expectedPath = Path.Combine(testBuild.IntermediateDirectory, $"{projectName}.GlobalUsings.g.cs");
		A.CallTo(() => fileSystemTools.FileExists(expectedPath)).Returns(true);

		var sources = await Gather();

		sources[2].FilePath.Should().Be(expectedPath);
	}

	[Fact]
	public async Task PlaceTheGlobalUsingsFileBeforeTheGeneratedFile()
	{
		var generatedUsingsPath = AddGeneratedUsingsFile(testBuild);
		var globalUsingsPath = AddGlobalUsingsFile(testBuild);

		var sources = await Gather();

		sources.Skip(2).Select(source => source.FilePath).Should().Equal([globalUsingsPath, generatedUsingsPath]);
	}

	[Fact]
	public async Task SkipGlobalUsingsFilesThatDoNotExist()
	{
		AddGeneratedUsingsFile(testBuild);

		var sources = await Gather();

		sources.Count.Should().Be(3);
	}

	[Fact]
	public async Task IncludeTheProductionProjectsUsingsWhenItIsADifferentProject()
	{
		var testGlobalUsingsPath = AddGlobalUsingsFile(testBuild);
		var testGeneratedUsingsPath = AddGeneratedUsingsFile(testBuild);
		var productionGlobalUsingsPath = AddGlobalUsingsFile(productionBuild);
		var productionGeneratedUsingsPath = AddGeneratedUsingsFile(productionBuild);

		var sources = await Gather();

		sources
			.Skip(2)
			.Select(source => source.FilePath)
			.Should()
			.Equal([testGlobalUsingsPath, testGeneratedUsingsPath, productionGlobalUsingsPath, productionGeneratedUsingsPath]);
	}

	[Fact]
	public async Task IncludeEachProjectsUsingsOnceWhenBothFilesShareAProject()
	{
		var sharedBuild = CreateProjectBuild();
		var globalUsingsPath = AddGlobalUsingsFile(sharedBuild);
		var generatedUsingsPath = AddGeneratedUsingsFile(sharedBuild);
		var sameProjectBuild = CopyWithProjectFilePath(sharedBuild, sharedBuild.ProjectFilePath.ToUpperInvariant());

		var sources = await gatherCompilationSources.Gather(productionPath, testsPath, sameProjectBuild, sharedBuild);

		sources.Skip(2).Select(source => source.FilePath).Should().Equal([globalUsingsPath, generatedUsingsPath]);
	}

	[Fact]
	public async Task ReadEachSourcesText()
	{
		AddGlobalUsingsFile(testBuild);
		AddGeneratedUsingsFile(productionBuild);

		var sources = await Gather();

		sources.Should().OnlyContain(source => source.Text == $"text of {source.FilePath}");
	}

	private Task<List<SourceFile>> Gather()
	{
		return gatherCompilationSources.Gather(productionPath, testsPath, productionBuild, testBuild);
	}

	private string AddGlobalUsingsFile(ProjectBuild build)
	{
		var path = Path.Combine(build.ProjectDirectory, "GlobalUsings.cs");

		A.CallTo(() => fileSystemTools.FileExists(path)).Returns(true);

		return path;
	}

	private string AddGeneratedUsingsFile(ProjectBuild build)
	{
		var projectName = Path.GetFileNameWithoutExtension(build.ProjectFilePath);
		var path = Path.Combine(build.IntermediateDirectory, $"{projectName}.GlobalUsings.g.cs");

		A.CallTo(() => fileSystemTools.FileExists(path)).Returns(true);

		return path;
	}

	private static ProjectBuild CreateProjectBuild()
	{
		var projectDirectory = Path.Combine(Path.GetTempPath(), Faker.Create<string>());
		var targetFramework = Faker.Create<string>();
		var configurationName = Faker.Create<string>();

		return new ProjectBuild
		{
			ProjectFilePath = Path.Combine(projectDirectory, $"{Faker.Create<string>()}.csproj"),
			ProjectDirectory = projectDirectory,
			AssemblyName = Faker.Create<string>(),
			TargetFramework = targetFramework,
			OutputDirectory = Path.Combine(projectDirectory, "bin", configurationName, targetFramework),
			IntermediateDirectory = Path.Combine(projectDirectory, "obj", configurationName, targetFramework),
		};
	}

	private static ProjectBuild CopyWithProjectFilePath(ProjectBuild build, string projectFilePath)
	{
		return new ProjectBuild
		{
			ProjectFilePath = projectFilePath,
			ProjectDirectory = build.ProjectDirectory,
			AssemblyName = build.AssemblyName,
			TargetFramework = build.TargetFramework,
			OutputDirectory = build.OutputDirectory,
			IntermediateDirectory = build.IntermediateDirectory,
		};
	}
}
