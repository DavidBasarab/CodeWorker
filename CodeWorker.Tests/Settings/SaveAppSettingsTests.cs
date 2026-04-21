using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Settings;

public class SaveAppSettingsTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly SaveAppSettings saveAppSettings;
	private readonly CodeWorkerSettings settings;

	public SaveAppSettingsTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		settings = new CodeWorkerSettings
		{
			Repositories = new List<RepositorySettings>
			{
				new()
				{
					Path = @"C:\Projects\my-api",
					Enabled = true,
					SettingsPath = @"C:\Projects\my-api\tasks\settings.json",
				},
			},
			Git = new GitSettings { CommitAfterEachTask = true, CommitMessagePrefix = "bot" },
			Claude = new ClaudeSettings { Model = "claude-sonnet-4-6", MaxTurns = 10 },
		};

		saveAppSettings = new SaveAppSettings(fileSystemTools, logger);
	}

	[Fact]
	public async Task WriteToAppSettingsJsonInBaseDirectory()
	{
		await saveAppSettings.Save(settings);

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>.That.EndsWith("appsettings.json"), A<string>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteSerializedJsonContainingRepositoryPath()
	{
		var capturedJson = string.Empty;

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>.Ignored, A<string>.Ignored))
			.Invokes((string _, string text) => capturedJson = text);

		await saveAppSettings.Save(settings);

		capturedJson.Should().Contain(@"C:\\Projects\\my-api");
	}

	[Fact]
	public async Task WriteIndentedJson()
	{
		var capturedJson = string.Empty;

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>.Ignored, A<string>.Ignored))
			.Invokes((string _, string text) => capturedJson = text);

		await saveAppSettings.Save(settings);

		capturedJson.Should().Contain("\n");
	}

	[Fact]
	public async Task WriteJsonContainingRepositoriesProperty()
	{
		var capturedJson = string.Empty;

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>.Ignored, A<string>.Ignored))
			.Invokes((string _, string text) => capturedJson = text);

		await saveAppSettings.Save(settings);

		capturedJson.Should().Contain("\"Repositories\"");
	}
}
