using FatCat.CodeWorker.Commands.Track;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Track;

public class TrackRepositoryTests
{
	private readonly ILoadAppSettings loadAppSettings;
	private readonly ISaveAppSettings saveAppSettings;
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly TrackRepository trackRepository;
	private readonly string repositoryPath = @"C:\Projects\my-api";
	private CodeWorkerSettings currentSettings;

	public TrackRepositoryTests()
	{
		loadAppSettings = A.Fake<ILoadAppSettings>();
		saveAppSettings = A.Fake<ISaveAppSettings>();
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		currentSettings = new CodeWorkerSettings { Repositories = new List<RepositorySettings>() };

		A.CallTo(() => loadAppSettings.Load()).ReturnsLazily(() => Task.FromResult(currentSettings));
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>._)).Returns(true);

		trackRepository = new TrackRepository(loadAppSettings, saveAppSettings, fileSystemTools, logger);
	}

	[Fact]
	public async Task LoadAppSettings()
	{
		await trackRepository.Track(repositoryPath);

		A.CallTo(() => loadAppSettings.Load()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SaveUpdatedSettings()
	{
		await trackRepository.Track(repositoryPath);

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AddRepositoryToTheList()
	{
		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await trackRepository.Track(repositoryPath);

		savedSettings.Repositories.Should().HaveCount(1);
	}

	[Fact]
	public async Task AddRepositoryWithGivenPath()
	{
		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await trackRepository.Track(repositoryPath);

		savedSettings.Repositories[0].Path.Should().Be(repositoryPath);
	}

	[Fact]
	public async Task AddRepositoryWithEnabledTrue()
	{
		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await trackRepository.Track(repositoryPath);

		savedSettings.Repositories[0].Enabled.Should().BeTrue();
	}

	[Fact]
	public async Task AddRepositoryWithSettingsPathUnderTasks()
	{
		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await trackRepository.Track(repositoryPath);

		savedSettings.Repositories[0].SettingsPath.Should().Be(@"C:\Projects\my-api\tasks\settings.json");
	}

	[Fact]
	public async Task NotAddDuplicateWhenAlreadyTracked()
	{
		currentSettings.Repositories.Add(new RepositorySettings { Path = repositoryPath, Enabled = true });

		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await trackRepository.Track(repositoryPath);

		savedSettings.Should().BeNull();
	}

	[Fact]
	public async Task MatchExistingPathCaseInsensitive()
	{
		currentSettings.Repositories.Add(new RepositorySettings { Path = @"c:\projects\my-api", Enabled = true });

		await trackRepository.Track(repositoryPath);

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotTrackWhenTasksFolderDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(@"C:\Projects\my-api\tasks")).Returns(false);

		await trackRepository.Track(repositoryPath);

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task PreserveExistingRepositoriesWhenAddingNew()
	{
		currentSettings.Repositories.Add(
			new RepositorySettings
			{
				Path = @"C:\Projects\other",
				Enabled = true,
				SettingsPath = "other-settings",
			}
		);

		CodeWorkerSettings savedSettings = null;

		A.CallTo(() => saveAppSettings.Save(A<CodeWorkerSettings>._)).Invokes((CodeWorkerSettings s) => savedSettings = s);

		await trackRepository.Track(repositoryPath);

		savedSettings.Repositories.Should().HaveCount(2);
	}
}
