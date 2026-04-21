using FatCat.CodeWorker.Commands.Untrack;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Untrack;

public class UntrackRepositoryTests
{
	private readonly ILoadAppSettings loadAppSettings;
	private readonly ISaveAppSettings saveAppSettings;
	private readonly ILogger logger;
	private readonly UntrackRepository untrackRepository;
	private readonly string repositoryPath = @"C:\Projects\my-api";
	private CodeWorkerSettings currentSettings;

	public UntrackRepositoryTests()
	{
		loadAppSettings = A.Fake<ILoadAppSettings>();
		saveAppSettings = A.Fake<ISaveAppSettings>();
		logger = A.Fake<ILogger>();

		currentSettings = new CodeWorkerSettings
		{
			Repositories = new List<RepositorySettings>
			{
				new() { Path = repositoryPath, Enabled = true },
				new() { Path = @"C:\Projects\other", Enabled = true },
			},
		};

		A.CallTo(() => loadAppSettings.Load()).ReturnsLazily(() => Task.FromResult(currentSettings));

		untrackRepository = new UntrackRepository(loadAppSettings, saveAppSettings, logger);
	}

	[Fact]
	public async Task LoadAppSettings()
	{
		await untrackRepository.Untrack(repositoryPath);

		A.CallTo(() => loadAppSettings.Load()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SaveUpdatedSettings()
	{
		await untrackRepository.Untrack(repositoryPath);

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RemoveMatchingRepository()
	{
		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await untrackRepository.Untrack(repositoryPath);

		savedSettings.Repositories.Should().HaveCount(1);
	}

	[Fact]
	public async Task PreserveOtherRepositories()
	{
		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await untrackRepository.Untrack(repositoryPath);

		savedSettings.Repositories[0].Path.Should().Be(@"C:\Projects\other");
	}

	[Fact]
	public async Task MatchPathCaseInsensitive()
	{
		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await untrackRepository.Untrack(@"c:\projects\MY-API");

		savedSettings.Repositories.Should().HaveCount(1);
	}

	[Fact]
	public async Task NotSaveWhenNoMatchFound()
	{
		await untrackRepository.Untrack(@"C:\Projects\does-not-exist");

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotSaveWhenRepositoriesIsNull()
	{
		currentSettings.Repositories = null;

		await untrackRepository.Untrack(repositoryPath);

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).MustNotHaveHappened();
	}
}
