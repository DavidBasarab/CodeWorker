using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class RunCommandTests
{
	private readonly ILoadAppSettings loadAppSettings;
	private readonly IProcessRepository processRepository;
	private readonly ILogger logger;
	private readonly RunCommand command;
	private CodeWorkerSettings appSettings;

	public RunCommandTests()
	{
		loadAppSettings = A.Fake<ILoadAppSettings>();
		processRepository = A.Fake<IProcessRepository>();
		logger = A.Fake<ILogger>();

		appSettings = new CodeWorkerSettings
		{
			Repositories = new List<RepositorySettings>
			{
				new() { Path = @"C:\Projects\my-api", Enabled = true },
				new() { Path = @"C:\Projects\my-frontend", Enabled = true },
			},
		};

		A.CallTo(() => loadAppSettings.Load()).Returns(Task.FromResult(appSettings));
		A.CallTo(() => processRepository.Process(A<RepositorySettings>.Ignored)).Returns(Task.CompletedTask);

		command = new RunCommand(loadAppSettings, processRepository, logger);
	}

	[Fact]
	public async Task LogThatTheRunCommandIsStarting()
	{
		await command.Execute(Array.Empty<string>());

		A.CallTo(() => logger.Information("Starting task runner")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LoadAppSettings()
	{
		await command.Execute(Array.Empty<string>());

		A.CallTo(() => loadAppSettings.Load()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessEachRepository()
	{
		await command.Execute(Array.Empty<string>());

		A.CallTo(() => processRepository.Process(A<RepositorySettings>.Ignored)).MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task ProcessFirstRepository()
	{
		await command.Execute(Array.Empty<string>());

		A.CallTo(() => processRepository.Process(A<RepositorySettings>.That.Matches(r => r.Path == @"C:\Projects\my-api")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessSecondRepository()
	{
		await command.Execute(Array.Empty<string>());

		A.CallTo(() => processRepository.Process(A<RepositorySettings>.That.Matches(r => r.Path == @"C:\Projects\my-frontend")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogCompletion()
	{
		await command.Execute(Array.Empty<string>());

		A.CallTo(() => logger.Information("Task runner complete")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleEmptyRepositoryList()
	{
		appSettings.Repositories = new List<RepositorySettings>();

		await command.Execute(Array.Empty<string>());

		A.CallTo(() => processRepository.Process(A<RepositorySettings>.Ignored)).MustNotHaveHappened();
	}
}
