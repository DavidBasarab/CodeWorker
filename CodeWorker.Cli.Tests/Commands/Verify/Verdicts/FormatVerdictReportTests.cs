using FatCat.CodeWorker.Cli.Commands.Verify.Gates;
using FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public class FormatVerdictReportTests
{
	private readonly FormatVerdictReport formatVerdictReport;

	public FormatVerdictReportTests()
	{
		formatVerdictReport = new FormatVerdictReport();
	}

	[Fact]
	public void StartWithTheGreenStatusAndMessage()
	{
		var verdict = VerdictWithStatus(VerdictStatus.Green);

		var lines = ReportLines(verdict);

		lines[0].Should().Be($"Verdict: GREEN — {verdict.Message}");
	}

	[Fact]
	public void StartWithTheBlockedStatusAndMessage()
	{
		var verdict = VerdictWithStatus(VerdictStatus.Blocked);

		var lines = ReportLines(verdict);

		lines[0].Should().Be($"Verdict: BLOCKED — {verdict.Message}");
	}

	[Fact]
	public void StartWithTheInvalidIntentStatusAndMessage()
	{
		var verdict = VerdictWithStatus(VerdictStatus.InvalidIntent);

		var lines = ReportLines(verdict);

		lines[0].Should().Be($"Verdict: INVALID INTENT — {verdict.Message}");
	}

	[Fact]
	public void ThrowForUnknownVerdictStatus()
	{
		var verdict = VerdictWithStatus((VerdictStatus)999);

		Action act = () => formatVerdictReport.Format(verdict);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WriteOnlyTheStatusLineWhenThereAreNoGates()
	{
		var verdict = VerdictWithGates();

		var report = formatVerdictReport.Format(verdict);

		report.Should().Be($"Verdict: BLOCKED — {verdict.Message}");
	}

	[Fact]
	public void WriteALinePerGate()
	{
		var verdict = VerdictWithGates(GateWithFindings(), GateWithFindings(), GateWithFindings());

		var lines = ReportLines(verdict);

		lines.Length.Should().Be(4);
	}

	[Fact]
	public void WriteTheGateStatusNameStageAndDuration()
	{
		var gate = GateWithFindings();

		var lines = ReportLines(VerdictWithGates(gate));

		lines[1].Should().Be($"  [{gate.Status}] {gate.Name} ({gate.Stage}) {gate.DurationMilliseconds} ms");
	}

	[Fact]
	public void WriteAFindingWithFileAndLine()
	{
		var finding = FindingOnLine(Faker.Create<int>());

		var lines = ReportLines(VerdictWithGates(GateWithFindings(finding)));

		lines[2].Should().Be($"    {finding.FilePath}:{finding.Line} — {finding.Reason}");
	}

	[Fact]
	public void WriteAFindingWithoutALine()
	{
		var finding = FindingOnLine(null);

		var lines = ReportLines(VerdictWithGates(GateWithFindings(finding)));

		lines[2].Should().Be($"    {finding.FilePath} — {finding.Reason}");
	}

	[Fact]
	public void WriteAFindingWithoutAFile()
	{
		var finding = FindingOnLine(null);

		finding.FilePath = null;

		var lines = ReportLines(VerdictWithGates(GateWithFindings(finding)));

		lines[2].Should().Be($"    {finding.Reason}");
	}

	[Fact]
	public void PlaceFindingsUnderTheirGate()
	{
		var firstFinding = FindingOnLine(Faker.Create<int>());
		var secondFinding = FindingOnLine(Faker.Create<int>());
		var firstGate = GateWithFindings(firstFinding, secondFinding);
		var secondGate = GateWithFindings();
		var verdict = VerdictWithGates(firstGate, secondGate);

		var report = formatVerdictReport.Format(verdict);

		report
			.Should()
			.Be(
				string.Join(
					Environment.NewLine,
					$"Verdict: BLOCKED — {verdict.Message}",
					ExpectedGateLine(firstGate),
					ExpectedFindingLine(firstFinding),
					ExpectedFindingLine(secondFinding),
					ExpectedGateLine(secondGate)
				)
			);
	}

	[Fact]
	public void KeepTheVerdictMessageOnOneLine()
	{
		var firstPart = Faker.Create<string>();
		var secondPart = Faker.Create<string>();
		var verdict = VerdictWithGates();

		verdict.Message = $"{firstPart}\nVerdict: GREEN — {secondPart}";

		var report = formatVerdictReport.Format(verdict);

		report.Should().Be($"Verdict: BLOCKED — {firstPart} Verdict: GREEN — {secondPart}");
	}

	[Fact]
	public void KeepAFindingReasonOnOneLine()
	{
		var firstPart = Faker.Create<string>();
		var secondPart = Faker.Create<string>();
		var finding = FindingOnLine(null);

		finding.Reason = $"{firstPart}\r\n{secondPart}";

		var lines = ReportLines(VerdictWithGates(GateWithFindings(finding)));

		lines[2].Should().Be($"    {finding.FilePath} — {firstPart} {secondPart}");
	}

	[Fact]
	public void KeepAFindingFilePathOnOneLine()
	{
		var firstPart = Faker.Create<string>();
		var secondPart = Faker.Create<string>();
		var finding = FindingOnLine(null);

		finding.FilePath = $"{firstPart}\n{secondPart}";

		var lines = ReportLines(VerdictWithGates(GateWithFindings(finding)));

		lines[2].Should().Be($"    {firstPart} {secondPart} — {finding.Reason}");
	}

	private static string ExpectedGateLine(GateVerdict gate)
	{
		return $"  [{gate.Status}] {gate.Name} ({gate.Stage}) {gate.DurationMilliseconds} ms";
	}

	private static string ExpectedFindingLine(GateFinding finding)
	{
		return $"    {finding.FilePath}:{finding.Line} — {finding.Reason}";
	}

	private string[] ReportLines(Verdict verdict)
	{
		return formatVerdictReport.Format(verdict).Split(Environment.NewLine);
	}

	private static Verdict VerdictWithStatus(VerdictStatus status)
	{
		var verdict = Faker.Create<Verdict>();

		verdict.Status = status;
		verdict.Gates = [];

		return verdict;
	}

	private static Verdict VerdictWithGates(params GateVerdict[] gates)
	{
		var verdict = VerdictWithStatus(VerdictStatus.Blocked);

		verdict.Gates = [.. gates];

		return verdict;
	}

	private static GateVerdict GateWithFindings(params GateFinding[] findings)
	{
		var gate = Faker.Create<GateVerdict>();

		gate.Findings = [.. findings];

		return gate;
	}

	private static GateFinding FindingOnLine(int? line)
	{
		var finding = Faker.Create<GateFinding>();

		finding.Line = line;

		return finding;
	}
}
