using FatCat.CodeWorker.Commands.List;
using FatCat.CodeWorker.Settings;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.List;

public class ListRepositoriesCommandTests
{
	private readonly ILoadAppSettings loadAppSettings;
	private readonly ILogger logger;
	private readonly ListRepositoriesCommand command;
	private CodeWorkerSettings currentSettings;

	public ListRepositoriesCommandTests()
	{
		loadAppSettings = A.Fake<ILoadAppSettings>();
		logger = A.Fake<ILogger>();

		currentSettings = new CodeWorkerSettings
		{
			Repositories = new List<RepositorySettings>
			{
				new()
				{
					Path = @"C:\Projects\my-api",
					Enabled = true,
					SettingsPath = @"C:\Projects\my-api\tasks\settings.json",
				},
				new()
				{
					Path = @"C:\Projects\my-frontend",
					Enabled = false,
					SettingsPath = @"C:\Projects\my-frontend\tasks\settings.json",
				},
			},
		};

		A.CallTo(() => loadAppSettings.Load()).ReturnsLazily(() => Task.FromResult(currentSettings));

		command = new ListRepositoriesCommand(loadAppSettings, logger);
	}

	[Fact]
	public async Task LoadAppSettings()
	{
		await command.Execute(new[] { "list" });

		A.CallTo(() => loadAppSettings.Load()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogFirstRepositoryPath()
	{
		await command.Execute(new[] { "list" });

		A.CallTo(() => logger.Information(A<string>.Ignored, @"C:\Projects\my-api", A<bool>.Ignored)).MustHaveHappened();
	}

	[Fact]
	public async Task LogSecondRepositoryPath()
	{
		await command.Execute(new[] { "list" });

		A.CallTo(() => logger.Information(A<string>.Ignored, @"C:\Projects\my-frontend", A<bool>.Ignored)).MustHaveHappened();
	}

	[Fact]
	public async Task LogCountOfTrackedRepositories()
	{
		await command.Execute(new[] { "list" });

		A.CallTo(() => logger.Information(A<string>.That.Contains("Tracked"), 2)).MustHaveHappened();
	}

	[Fact]
	public async Task LogWhenNoRepositoriesAreTracked()
	{
		currentSettings.Repositories = new List<RepositorySettings>();

		await command.Execute(new[] { "list" });

		A.CallTo(() => logger.Information("No repositories are being tracked")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogWhenRepositoriesIsNull()
	{
		currentSettings.Repositories = null;

		await command.Execute(new[] { "list" });

		A.CallTo(() => logger.Information("No repositories are being tracked")).MustHaveHappenedOnceExactly();
	}
}
