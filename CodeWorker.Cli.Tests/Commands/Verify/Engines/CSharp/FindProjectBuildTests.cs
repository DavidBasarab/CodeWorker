using System.IO.Abstractions;
using FatCat.CodeWorker.Cli.Commands.Verify.Engines;
using FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;
using FatCat.Toolkit;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public class FindProjectBuildTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly FindProjectBuild findProjectBuild;
	private readonly string projectDirectory;
	private readonly string projectName;
	private readonly string projectFilePath;
	private readonly string sourceFilePath;
	private readonly string targetFramework;
	private readonly string configurationName;
	private readonly DateTime buildTime;

	public FindProjectBuildTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();

		projectDirectory = Path.Combine(Path.GetTempPath(), Faker.Create<string>(), Faker.Create<string>());
		projectName = Faker.Create<string>();
		projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");
		sourceFilePath = Path.Combine(projectDirectory, $"{Faker.Create<string>()}.cs");
		targetFramework = Faker.Create<string>();
		configurationName = Faker.Create<string>();
		buildTime = Faker.Create<DateTime>();

		findProjectBuild = new FindProjectBuild(fileSystemTools);
	}

	[Fact]
	public async Task FindTheProjectInTheSourceFilesFolder()
	{
		CreateBuiltProject();

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.ProjectFilePath.Should().Be(projectFilePath);
	}

	[Fact]
	public async Task FindTheProjectInAnAncestorFolder()
	{
		CreateBuiltProject();
		var nestedSourceFilePath = Path.Combine(
			projectDirectory,
			Faker.Create<string>(),
			Faker.Create<string>(),
			$"{Faker.Create<string>()}.cs"
		);

		var result = await findProjectBuild.Find(nestedSourceFilePath);

		result.Build.ProjectFilePath.Should().Be(projectFilePath);
	}

	[Fact]
	public async Task FindTheProjectInTheCurrentDirectoryForARelativeSourcePath()
	{
		var currentDirectoryProjectFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"{Faker.Create<string>()}.csproj");
		CreateProjectFile(currentDirectoryProjectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));

		await findProjectBuild.Find($"{Faker.Create<string>()}.cs");

		A.CallTo(() => fileSystemTools.ReadAllText(currentDirectoryProjectFilePath)).MustHaveHappened();
	}

	[Fact]
	public async Task SetTheProjectDirectoryToTheProjectFilesFolder()
	{
		CreateBuiltProject();
		var nestedSourceFilePath = Path.Combine(projectDirectory, Faker.Create<string>(), $"{Faker.Create<string>()}.cs");

		var result = await findProjectBuild.Find(nestedSourceFilePath);

		result.Build.ProjectDirectory.Should().Be(projectDirectory);
	}

	[Fact]
	public async Task StopAtTheNearestProject()
	{
		CreateBuiltProject();
		var parentDirectory = Path.GetDirectoryName(projectDirectory);
		A.CallTo(() => fileSystemTools.GetFiles(parentDirectory))
			.Returns([Path.Combine(parentDirectory, $"{Faker.Create<string>()}.csproj")]);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.ProjectFilePath.Should().Be(projectFilePath);
	}

	[Fact]
	public async Task ReturnProjectNotFoundWhenNoAncestorHasAProject()
	{
		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.ProjectNotFound);
	}

	[Fact]
	public async Task NameTheSourceFileWhenNoProjectIsFound()
	{
		var result = await findProjectBuild.Find(sourceFilePath);

		result.Message.Should().Be($"No project file (.csproj) was found for {sourceFilePath}");
	}

	[Fact]
	public async Task ReturnMultipleProjectsFoundWhenAFolderHasTwoProjects()
	{
		CreateBuiltProject();
		AddSecondProjectFile();

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.MultipleProjectsFound);
	}

	[Fact]
	public async Task NameTheFolderWhenItHasTwoProjects()
	{
		CreateBuiltProject();
		AddSecondProjectFile();

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Message.Should().Be($"More than one project file (.csproj) was found in {projectDirectory}");
	}

	[Fact]
	public async Task IgnoreFilesThatAreNotProjects()
	{
		CreateBuiltProject();
		A.CallTo(() => fileSystemTools.GetFiles(projectDirectory))
			.Returns([
				Path.Combine(projectDirectory, $"{Faker.Create<string>()}.cs"),
				projectFilePath,
				Path.Combine(projectDirectory, $"{Faker.Create<string>()}.sln"),
				Path.Combine(projectDirectory, $"{Faker.Create<string>()}.csproj.user"),
			]);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.ProjectFilePath.Should().Be(projectFilePath);
	}

	[Fact]
	public async Task MatchTheProjectExtensionIgnoringCase()
	{
		var upperCaseProjectFilePath = Path.Combine(projectDirectory, $"{projectName}.CSPROJ");
		CreateProjectFile(upperCaseProjectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));
		CreateConfiguration(configurationName, projectName, buildTime);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.ProjectFilePath.Should().Be(upperCaseProjectFilePath);
	}

	[Fact]
	public async Task ReturnProjectFileUnreadableWhenTheXmlIsMalformed()
	{
		CreateProjectFile(projectFilePath, "<Project><PropertyGroup>");

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.ProjectFileUnreadable);
	}

	[Fact]
	public async Task ReturnProjectFileUnreadableWhenTheProjectFileIsEmpty()
	{
		CreateProjectFile(projectFilePath, string.Empty);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.ProjectFileUnreadable);
	}

	[Fact]
	public async Task NameTheProjectFileWhenItIsUnreadable()
	{
		CreateProjectFile(projectFilePath, "<Project><PropertyGroup>");

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Message.Should().StartWith($"The project file could not be read as XML: {projectFilePath}");
	}

	[Fact]
	public async Task ReturnTargetFrameworkNotFoundWhenTheElementIsMissing()
	{
		CreateProjectFile(projectFilePath, ProjectXml("<OutputType>Library</OutputType>"));

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.TargetFrameworkNotFound);
	}

	[Fact]
	public async Task ReturnTargetFrameworkNotFoundWhenTheElementIsBlank()
	{
		CreateProjectFile(projectFilePath, ProjectXml("<TargetFramework>   </TargetFramework>"));

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.TargetFrameworkNotFound);
	}

	[Fact]
	public async Task ReturnTargetFrameworkNotFoundWhenOnlyTargetFrameworksIsSet()
	{
		CreateProjectFile(
			projectFilePath,
			ProjectXml($"<TargetFrameworks>{Faker.Create<string>()};{targetFramework}</TargetFrameworks>")
		);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.TargetFrameworkNotFound);
	}

	[Fact]
	public async Task NameTheProjectFileWhenTheTargetFrameworkIsMissing()
	{
		CreateProjectFile(projectFilePath, ProjectXml("<OutputType>Library</OutputType>"));

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Message.Should().Be($"No <TargetFramework> was found in the project file {projectFilePath}");
	}

	[Fact]
	public async Task ReadTheTargetFramework()
	{
		CreateBuiltProject();

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.TargetFramework.Should().Be(targetFramework);
	}

	[Fact]
	public async Task TrimTheTargetFramework()
	{
		CreateProjectFile(projectFilePath, ProjectXml($"<TargetFramework>  {targetFramework}  </TargetFramework>"));
		CreateConfiguration(configurationName, projectName, buildTime);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.TargetFramework.Should().Be(targetFramework);
	}

	[Fact]
	public async Task ReadTheTargetFrameworkFromAProjectWithAnXmlNamespace()
	{
		CreateProjectFile(
			projectFilePath,
			$"""<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><PropertyGroup><TargetFramework>{targetFramework}</TargetFramework></PropertyGroup></Project>"""
		);
		CreateConfiguration(configurationName, projectName, buildTime);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.TargetFramework.Should().Be(targetFramework);
	}

	[Fact]
	public async Task ReadTheAssemblyName()
	{
		var assemblyName = Faker.Create<string>();
		CreateProjectFile(
			projectFilePath,
			ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework><AssemblyName>{assemblyName}</AssemblyName>")
		);
		CreateConfiguration(configurationName, assemblyName, buildTime);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.AssemblyName.Should().Be(assemblyName);
	}

	[Fact]
	public async Task DefaultTheAssemblyNameToTheProjectFileName()
	{
		CreateBuiltProject();

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.AssemblyName.Should().Be(projectName);
	}

	[Fact]
	public async Task DefaultTheAssemblyNameWhenTheElementIsBlank()
	{
		CreateProjectFile(
			projectFilePath,
			ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework><AssemblyName> </AssemblyName>")
		);
		CreateConfiguration(configurationName, projectName, buildTime);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.AssemblyName.Should().Be(projectName);
	}

	[Fact]
	public async Task UseTheOutputFolderForTheProjectsTargetFramework()
	{
		CreateProjectFile(projectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));
		var staleConfigurationDirectory = Path.Combine(projectDirectory, "bin", Faker.Create<string>());
		var staleAssemblyPath = Path.Combine(staleConfigurationDirectory, Faker.Create<string>(), $"{projectName}.dll");
		A.CallTo(() => fileSystemTools.GetDirectories(Path.Combine(projectDirectory, "bin")))
			.Returns([staleConfigurationDirectory, Path.Combine(projectDirectory, "bin", configurationName)]);
		A.CallTo(() => fileSystemTools.FileExists(staleAssemblyPath)).Returns(true);
		SetLastWriteTime(staleAssemblyPath, buildTime);
		MarkAssemblyBuilt(configurationName, projectName, buildTime.AddDays(-1));

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.OutputDirectory.Should().Be(Path.Combine(projectDirectory, "bin", configurationName, targetFramework));
	}

	[Fact]
	public async Task PickTheMostRecentlyBuiltConfiguration()
	{
		var newestConfigurationName = Faker.Create<string>();
		CreateProjectFile(projectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));
		CreateConfigurations(
			(Faker.Create<string>(), buildTime.AddHours(-2)),
			(newestConfigurationName, buildTime),
			(Faker.Create<string>(), buildTime.AddHours(-1))
		);

		var result = await findProjectBuild.Find(sourceFilePath);

		result
			.Build.OutputDirectory.Should()
			.Be(Path.Combine(projectDirectory, "bin", newestConfigurationName, targetFramework));
	}

	[Fact]
	public async Task LookForTheConfigurationsInTheProjectsBinFolder()
	{
		CreateBuiltProject();

		await findProjectBuild.Find(sourceFilePath);

		A.CallTo(() => fileSystemTools.GetDirectories(Path.Combine(projectDirectory, "bin"))).MustHaveHappened();
	}

	[Fact]
	public async Task ReturnBuildOutputNotFoundWhenNoConfigurationHasTheAssembly()
	{
		CreateProjectFile(projectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));
		A.CallTo(() => fileSystemTools.GetDirectories(Path.Combine(projectDirectory, "bin")))
			.Returns([
				Path.Combine(projectDirectory, "bin", Faker.Create<string>()),
				Path.Combine(projectDirectory, "bin", Faker.Create<string>()),
			]);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.BuildOutputNotFound);
	}

	[Fact]
	public async Task ReturnBuildOutputNotFoundWhenThereIsNoBinFolder()
	{
		CreateProjectFile(projectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.BuildOutputNotFound);
	}

	[Fact]
	public async Task NameTheProjectTargetFrameworkAndAssemblyWhenNoBuildOutputIsFound()
	{
		CreateProjectFile(projectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));

		var result = await findProjectBuild.Find(sourceFilePath);

		result
			.Message.Should()
			.Be(
				$"No build output was found for {projectFilePath}: expected bin/<configuration>/{targetFramework}/{projectName}.dll (build the project first)"
			);
	}

	[Fact]
	public async Task SetTheIntermediateDirectoryForTheChosenConfiguration()
	{
		var newestConfigurationName = Faker.Create<string>();
		CreateProjectFile(projectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));
		CreateConfigurations((Faker.Create<string>(), buildTime.AddHours(-1)), (newestConfigurationName, buildTime));

		var result = await findProjectBuild.Find(sourceFilePath);

		result
			.Build.IntermediateDirectory.Should()
			.Be(Path.Combine(projectDirectory, "obj", newestConfigurationName, targetFramework));
	}

	[Fact]
	public async Task UseTheFirstTargetFrameworkElement()
	{
		CreateProjectFile(
			projectFilePath,
			ProjectXml(
				$"<TargetFramework>{targetFramework}</TargetFramework><TargetFramework>{Faker.Create<string>()}</TargetFramework>"
			)
		);
		CreateConfiguration(configurationName, projectName, buildTime);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.TargetFramework.Should().Be(targetFramework);
	}

	[Fact]
	public async Task UseTheFirstAssemblyNameElement()
	{
		var assemblyName = Faker.Create<string>();
		CreateProjectFile(
			projectFilePath,
			ProjectXml(
				$"<TargetFramework>{targetFramework}</TargetFramework><AssemblyName>{assemblyName}</AssemblyName><AssemblyName>{Faker.Create<string>()}</AssemblyName>"
			)
		);
		CreateConfiguration(configurationName, assemblyName, buildTime);

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Build.AssemblyName.Should().Be(assemblyName);
	}

	[Fact]
	public async Task ReturnNoErrorWhenFound()
	{
		CreateBuiltProject();

		var result = await findProjectBuild.Find(sourceFilePath);

		result.Error.Should().Be(EngineError.None);
	}

	private void CreateBuiltProject()
	{
		CreateProjectFile(projectFilePath, ProjectXml($"<TargetFramework>{targetFramework}</TargetFramework>"));
		CreateConfiguration(configurationName, projectName, buildTime);
	}

	private void CreateProjectFile(string path, string xml)
	{
		A.CallTo(() => fileSystemTools.GetFiles(Path.GetDirectoryName(path))).Returns([path]);
		A.CallTo(() => fileSystemTools.ReadAllText(path)).Returns(xml);
	}

	private void AddSecondProjectFile()
	{
		A.CallTo(() => fileSystemTools.GetFiles(projectDirectory))
			.Returns([projectFilePath, Path.Combine(projectDirectory, $"{Faker.Create<string>()}.csproj")]);
	}

	private void CreateConfiguration(string configurationName, string assemblyName, DateTime lastWriteTimeUtc)
	{
		A.CallTo(() => fileSystemTools.GetDirectories(Path.Combine(projectDirectory, "bin")))
			.Returns([Path.Combine(projectDirectory, "bin", configurationName)]);
		MarkAssemblyBuilt(configurationName, assemblyName, lastWriteTimeUtc);
	}

	private void CreateConfigurations(params (string Name, DateTime LastWriteTimeUtc)[] configurations)
	{
		A.CallTo(() => fileSystemTools.GetDirectories(Path.Combine(projectDirectory, "bin")))
			.Returns(
				configurations.Select(configuration => Path.Combine(projectDirectory, "bin", configuration.Name)).ToList()
			);

		foreach (var configuration in configurations)
		{
			MarkAssemblyBuilt(configuration.Name, projectName, configuration.LastWriteTimeUtc);
		}
	}

	private void MarkAssemblyBuilt(string configurationName, string assemblyName, DateTime lastWriteTimeUtc)
	{
		var assemblyPath = Path.Combine(projectDirectory, "bin", configurationName, targetFramework, $"{assemblyName}.dll");

		A.CallTo(() => fileSystemTools.FileExists(assemblyPath)).Returns(true);
		SetLastWriteTime(assemblyPath, lastWriteTimeUtc);
	}

	private void SetLastWriteTime(string path, DateTime lastWriteTimeUtc)
	{
		var fileInfo = A.Fake<IFileInfo>();

		A.CallTo(() => fileInfo.LastWriteTimeUtc).Returns(lastWriteTimeUtc);
		A.CallTo(() => fileSystemTools.GetFileMetaData(path)).Returns(fileInfo);
	}

	private static string ProjectXml(string properties)
	{
		return $"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>{properties}</PropertyGroup></Project>""";
	}
}
