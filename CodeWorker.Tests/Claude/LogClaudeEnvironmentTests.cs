using FatCat.CodeWorker.Claude;
using FatCat.CodeWorker.Process;
using Serilog;

namespace Testing.FatCat.CodeWorker.Claude;

public class LogClaudeEnvironmentTests
{
	private readonly IRunProcess runProcess;
	private readonly ILogger logger;
	private readonly LogClaudeEnvironment logClaudeEnvironment;
	private readonly List<ProcessSettings> capturedSettings;
	private ProcessResult versionResult;
	private ProcessResult doctorResult;

	public LogClaudeEnvironmentTests()
	{
		runProcess = A.Fake<IRunProcess>();
		logger = A.Fake<ILogger>();

		capturedSettings = new List<ProcessSettings>();

		versionResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "1.2.3 (Claude Code)" },
		};
		doctorResult = new ProcessResult
		{
			ExitCode = 0,
			OutputLines = new List<string> { "All good." },
		};

		A.CallTo(() => runProcess.Run(A<ProcessSettings>._))
			.ReturnsLazily(
				(ProcessSettings settings) =>
				{
					capturedSettings.Add(settings);

					return settings.Arguments.Contains("--version")
						? Task.FromResult(versionResult)
						: Task.FromResult(doctorResult);
				}
			);

		logClaudeEnvironment = new LogClaudeEnvironment(runProcess, logger);
	}

	[Fact]
	public async Task RunClaudeVersion()
	{
		await logClaudeEnvironment.Log();

		capturedSettings.Should().Contain(settings => settings.FileName == "claude" && settings.Arguments == "--version");
	}

	[Fact]
	public async Task RunClaudeDoctor()
	{
		await logClaudeEnvironment.Log();

		capturedSettings.Should().Contain(settings => settings.FileName == "claude" && settings.Arguments == "doctor");
	}

	[Fact]
	public async Task SetAShortTimeoutOnDiagnosticCommands()
	{
		await logClaudeEnvironment.Log();

		capturedSettings.Should().OnlyContain(settings => settings.TimeoutMilliseconds > 0);
	}

	[Fact]
	public async Task LogTheVersionOutput()
	{
		versionResult.OutputLines = new List<string> { "9.9.9 (Claude Code)" };

		await logClaudeEnvironment.Log();

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("claude"),
					A<string>._,
					A<int>._,
					A<string>.That.Contains("9.9.9 (Claude Code)")
				)
			)
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogTheDoctorOutput()
	{
		doctorResult.OutputLines = new List<string> { "Installation healthy" };

		await logClaudeEnvironment.Log();

		A.CallTo(() =>
				logger.Information(
					A<string>.That.Contains("claude"),
					A<string>._,
					A<int>._,
					A<string>.That.Contains("Installation healthy")
				)
			)
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogWarningWhenDiagnosticFailsToStart()
	{
		versionResult.FailedToStart = true;
		versionResult.ExitCode = -1;
		versionResult.ErrorLines = new List<string> { "claude not found" };

		await logClaudeEnvironment.Log();

		A.CallTo(() => logger.Warning(A<string>.That.Contains("claude"), A<object[]>._)).MustHaveHappened();
	}

	[Fact]
	public async Task LogTheStartOfDiagnostics()
	{
		await logClaudeEnvironment.Log();

		A.CallTo(() => logger.Information(A<string>.That.Contains("Claude environment"))).MustHaveHappened();
	}
}
