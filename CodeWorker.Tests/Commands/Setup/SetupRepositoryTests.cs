using FatCat.CodeWorker.Commands.Setup;
using FatCat.CodeWorker.Commands.Track;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Setup;

public class SetupRepositoryTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly IReadEmbeddedResource readEmbeddedResource;
	private readonly ITrackRepository trackRepository;
	private readonly ILogger logger;
	private readonly SetupRepository setupRepository;
	private readonly string repositoryPath = @"C:\Projects\my-api";

	public SetupRepositoryTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		readEmbeddedResource = A.Fake<IReadEmbeddedResource>();
		trackRepository = A.Fake<ITrackRepository>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => readEmbeddedResource.Read("TasksReadme.md")).Returns("# Tasks\n\n01-example");
		A.CallTo(() => readEmbeddedResource.Read("defaultSettings.json"))
			.Returns("{\"Enabled\": true, \"Claude\": {}, \"Tasks\": {}, \"Notifications\": {}}");

		setupRepository = new SetupRepository(fileSystemTools, readEmbeddedResource, trackRepository, logger);
	}

	[Fact]
	public async Task TrackTheRepositoryAfterSetup()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => trackRepository.Track(repositoryPath)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateTheTodoDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\todo")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateTheDoneDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\done")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateTheBlockedDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\blocked")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateTheFailedDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\failed")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateThePendingDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\pending")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateTheReferenceDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\reference")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToTodoDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\todo\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToDoneDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\done\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToBlockedDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\blocked\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToFailedDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\failed\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToPendingDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\pending\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToReferenceDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\reference\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteReadmeToTasksDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\README.md", A<string>.That.Contains("# Tasks")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReadmeContainsOrderingExplanation()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => readEmbeddedResource.Read("TasksReadme.md")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteSettingsJsonToTasksDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\settings.json", A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReadSettingsFromEmbeddedResource()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => readEmbeddedResource.Read("defaultSettings.json")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateTheLogsDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\logs")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToLogsDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\logs\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}
}
