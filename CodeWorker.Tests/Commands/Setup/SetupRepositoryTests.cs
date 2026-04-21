using FatCat.CodeWorker.Commands.Setup;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Setup;

public class SetupRepositoryTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly SetupRepository setupRepository;
	private readonly string repositoryPath = @"C:\Projects\my-api";

	public SetupRepositoryTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		setupRepository = new SetupRepository(fileSystemTools, logger);
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
	public async Task WriteReadmeToTasksDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\README.md", A<string>.That.Contains("# Tasks")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReadmeContainsOrderingExplanation()
	{
		var capturedContent = string.Empty;

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>.That.EndsWith("README.md"), A<string>.Ignored))
			.Invokes((string path, string text) => capturedContent = text);

		await setupRepository.Setup(repositoryPath);

		capturedContent.Should().Contain("01-");
	}

	[Fact]
	public async Task CreateThePendingDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\pending")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToPendingDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\pending\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CreateTheFailedDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.EnsureDirectory(@"C:\Projects\my-api\tasks\failed")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteGitKeepToFailedDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\failed\.gitkeep", string.Empty))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteSettingsJsonToTasksDirectory()
	{
		await setupRepository.Setup(repositoryPath);

		A.CallTo(() => fileSystemTools.WriteAllText(@"C:\Projects\my-api\tasks\settings.json", A<string>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SettingsJsonContainsExpectedStructure()
	{
		var capturedContent = string.Empty;

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>.That.EndsWith("settings.json"), A<string>.Ignored))
			.Invokes((string path, string text) => capturedContent = text);

		await setupRepository.Setup(repositoryPath);

		capturedContent.Should().Contain("\"Enabled\"");
		capturedContent.Should().Contain("\"Claude\"");
		capturedContent.Should().Contain("\"Tasks\"");
		capturedContent.Should().Contain("\"Notifications\"");
	}
}
