using FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;
using FatCat.CodeWorker.Cli.Process;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public class DeliverVerdictTests
{
	private const string ReportTemplate = "{Report:l}";

	private readonly ISerializeVerdict serializeVerdict;
	private readonly IFormatVerdictReport formatVerdictReport;
	private readonly IFileSystemTools fileSystemTools;
	private readonly ISetExitCode setExitCode;
	private readonly ILogger logger;
	private readonly DeliverVerdict deliverVerdict;

	private readonly Verdict verdict;
	private readonly string serializedVerdict;
	private readonly string report;
	private readonly string verdictPath;

	public DeliverVerdictTests()
	{
		serializeVerdict = A.Fake<ISerializeVerdict>();
		formatVerdictReport = A.Fake<IFormatVerdictReport>();
		fileSystemTools = A.Fake<IFileSystemTools>();
		setExitCode = A.Fake<ISetExitCode>();
		logger = A.Fake<ILogger>();

		verdict = Faker.Create<Verdict>();
		verdict.Status = VerdictStatus.Green;

		serializedVerdict = Faker.Create<string>();
		report = Faker.Create<string>();
		verdictPath = Path.Combine(Path.GetTempPath(), Faker.Create<string>());

		A.CallTo(() => serializeVerdict.Serialize(verdict)).Returns(serializedVerdict);
		A.CallTo(() => formatVerdictReport.Format(verdict)).Returns(report);

		deliverVerdict = new DeliverVerdict(serializeVerdict, formatVerdictReport, fileSystemTools, setExitCode, logger);
	}

	[Fact]
	public async Task WriteTheSerializedVerdictToTheVerdictPath()
	{
		await deliverVerdict.Deliver(verdict, verdictPath);

		A.CallTo(() => fileSystemTools.WriteAllText(verdictPath, serializedVerdict)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WriteARelativeVerdictPathUnderTheCurrentDirectory()
	{
		var relativeVerdictPath = Faker.Create<string>();

		await deliverVerdict.Deliver(verdict, relativeVerdictPath);

		A.CallTo(() =>
				fileSystemTools.WriteAllText(
					Path.Combine(Directory.GetCurrentDirectory(), relativeVerdictPath),
					serializedVerdict
				)
			)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotWriteAVerdictFileWhenNoPathIsGiven()
	{
		await deliverVerdict.Deliver(verdict, null);

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task NotWriteAVerdictFileWhenThePathIsBlank()
	{
		await deliverVerdict.Deliver(verdict, "   ");

		A.CallTo(() => fileSystemTools.WriteAllText(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task LogTheFormattedReport()
	{
		await deliverVerdict.Deliver(verdict, verdictPath);

		A.CallTo(() => logger.Information(ReportTemplate, report)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExitWithSuccessWhenGreen()
	{
		verdict.Status = VerdictStatus.Green;

		await deliverVerdict.Deliver(verdict, verdictPath);

		A.CallTo(() => setExitCode.Set(CliExitCode.Success)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExitWithBlockedWhenBlocked()
	{
		verdict.Status = VerdictStatus.Blocked;

		await deliverVerdict.Deliver(verdict, verdictPath);

		A.CallTo(() => setExitCode.Set(CliExitCode.Blocked)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExitWithInvalidInputWhenTheIntentIsInvalid()
	{
		verdict.Status = VerdictStatus.InvalidIntent;

		await deliverVerdict.Deliver(verdict, verdictPath);

		A.CallTo(() => setExitCode.Set(CliExitCode.InvalidInput)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowForAnUnknownVerdictStatus()
	{
		verdict.Status = (VerdictStatus)999;

		Func<Task> act = () => deliverVerdict.Deliver(verdict, verdictPath);

		act.Should().ThrowAsync<ArgumentOutOfRangeException>();
	}
}
