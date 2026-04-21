using FatCat.CodeWorker.Commands;
using FatCat.CodeWorker.FileSystem;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands;

public class ResolveRepositoryPathTests
{
	private readonly ILoadAppSettings loadAppSettings;
	private readonly IGetWorkingDirectory getWorkingDirectory;
	private readonly ILogger logger;
	private readonly ResolveRepositoryPath resolveRepositoryPath;
	private readonly string workingDirectory = @"C:\Projects\current";
	private CodeWorkerSettings currentSettings;

	public ResolveRepositoryPathTests()
	{
		loadAppSettings = A.Fake<ILoadAppSettings>();
		getWorkingDirectory = A.Fake<IGetWorkingDirectory>();
		logger = A.Fake<ILogger>();

		currentSettings = new CodeWorkerSettings
		{
			Repositories = new List<RepositorySettings>
			{
				new() { Path = @"C:\Projects\my-api" },
				new() { Path = @"C:\Projects\my-frontend" },
			},
		};

		A.CallTo(() => loadAppSettings.Load()).ReturnsLazily(() => Task.FromResult(currentSettings));
		A.CallTo(() => getWorkingDirectory.GetWorkingDirectory()).Returns(workingDirectory);

		resolveRepositoryPath = new ResolveRepositoryPath(loadAppSettings, getWorkingDirectory, logger);
	}

	[Fact]
	public async Task ReturnWorkingDirectoryWhenNoExtraArgument()
	{
		var result = await resolveRepositoryPath.Resolve(new[] { "track" });

		result.Should().Be(workingDirectory);
	}

	[Fact]
	public async Task ReturnResolvedPathWhenArgumentLooksLikePath()
	{
		var result = await resolveRepositoryPath.Resolve(new[] { "track", @"C:\Projects\new-repo" });

		result.Should().Be(@"C:\Projects\new-repo");
	}

	[Fact]
	public async Task ReturnMatchedTrackedPathWhenNameMatchesFolderName()
	{
		var result = await resolveRepositoryPath.Resolve(new[] { "track", "my-api" });

		result.Should().Be(@"C:\Projects\my-api");
	}

	[Fact]
	public async Task MatchTrackedPathCaseInsensitive()
	{
		var result = await resolveRepositoryPath.Resolve(new[] { "track", "MY-API" });

		result.Should().Be(@"C:\Projects\my-api");
	}

	[Fact]
	public async Task ReturnArgumentWhenNoTrackedRepositoryMatchesName()
	{
		var result = await resolveRepositoryPath.Resolve(new[] { "track", "unknown-name" });

		result.Should().Be("unknown-name");
	}

	[Fact]
	public async Task HandleEmptyRepositoriesList()
	{
		currentSettings.Repositories = new List<RepositorySettings>();

		var result = await resolveRepositoryPath.Resolve(new[] { "track" });

		result.Should().Be(workingDirectory);
	}

	[Fact]
	public async Task HandleNullRepositoriesList()
	{
		currentSettings.Repositories = null;

		var result = await resolveRepositoryPath.Resolve(new[] { "track", "my-api" });

		result.Should().Be("my-api");
	}
}
