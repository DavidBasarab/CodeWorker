using FatCat.CodeWorker.Cli.Commands.Verify.Gates;
using FatCat.CodeWorker.Cli.Commands.Verify.Intent;
using FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public class BuildVerdictTests
{
	private readonly BuildVerdict buildVerdict;

	public BuildVerdictTests()
	{
		buildVerdict = new BuildVerdict();
	}

	private static GatePipelineResult PipelineOf(bool isBlocked, params GateResult[] gateResults)
	{
		var pipelineResult = Faker.Create<GatePipelineResult>();

		pipelineResult.IsBlocked = isBlocked;
		pipelineResult.GateResults = [.. gateResults];

		return pipelineResult;
	}

	private static GateResult GateWithStatus(GateStatus status)
	{
		var gateResult = Faker.Create<GateResult>();

		gateResult.Status = status;

		return gateResult;
	}

	private static GateResult FailedGate(string gateName, params GateFinding[] findings)
	{
		var gateResult = GateWithStatus(GateStatus.Failed);

		gateResult.GateName = gateName;
		gateResult.Findings = [.. findings];

		return gateResult;
	}

	private static GateFinding Finding(string reason, string filePath, int? line)
	{
		return new GateFinding
		{
			Reason = reason,
			FilePath = filePath,
			Line = line,
		};
	}

	private static IntentResult InvalidIntent()
	{
		var intentResult = Faker.Create<IntentResult>();

		intentResult.IsValid = false;

		return intentResult;
	}

	[Fact]
	public void ReturnGreenWhenThePipelineIsNotBlocked()
	{
		var verdict = buildVerdict.Build(PipelineOf(false, GateWithStatus(GateStatus.Passed)));

		verdict.Status.Should().Be(VerdictStatus.Green);
	}

	[Fact]
	public void ReturnBlockedWhenThePipelineIsBlocked()
	{
		var verdict = buildVerdict.Build(PipelineOf(true, FailedGate(Faker.Create<string>())));

		verdict.Status.Should().Be(VerdictStatus.Blocked);
	}

	[Fact]
	public void SayNoGateFailedWhenGreen()
	{
		var verdict = buildVerdict.Build(PipelineOf(false, GateWithStatus(GateStatus.Passed)));

		verdict.Message.Should().Be("No gate failed.");
	}

	[Fact]
	public void NameTheFailedGateAndItsFirstFindingWhenBlocked()
	{
		var firstFinding = Finding(Faker.Create<string>(), Faker.Create<string>(), Faker.RandomInt(1, 1_000));
		var failedGate = FailedGate(Faker.Create<string>(), firstFinding, Faker.Create<GateFinding>());

		var verdict = buildVerdict.Build(PipelineOf(true, GateWithStatus(GateStatus.Warning), failedGate));

		verdict
			.Message.Should()
			.Be($"{failedGate.GateName} failed: {firstFinding.Reason} ({firstFinding.FilePath}:{firstFinding.Line})");
	}

	[Fact]
	public void UseTheFirstFailedGateWhenSeveralFailed()
	{
		var firstFailedGate = FailedGate(Faker.Create<string>());
		var secondFailedGate = FailedGate(Faker.Create<string>());

		var verdict = buildVerdict.Build(PipelineOf(true, firstFailedGate, secondFailedGate));

		verdict.Message.Should().Be($"{firstFailedGate.GateName} failed.");
	}

	[Fact]
	public void OmitTheLineWhenTheFindingHasNone()
	{
		var finding = Finding(Faker.Create<string>(), Faker.Create<string>(), null);
		var failedGate = FailedGate(Faker.Create<string>(), finding);

		var verdict = buildVerdict.Build(PipelineOf(true, failedGate));

		verdict.Message.Should().Be($"{failedGate.GateName} failed: {finding.Reason} ({finding.FilePath})");
	}

	[Fact]
	public void OmitTheLocationWhenTheFindingHasNoFile()
	{
		var finding = Finding(Faker.Create<string>(), null, Faker.RandomInt(1, 1_000));
		var failedGate = FailedGate(Faker.Create<string>(), finding);

		var verdict = buildVerdict.Build(PipelineOf(true, failedGate));

		verdict.Message.Should().Be($"{failedGate.GateName} failed: {finding.Reason}");
	}

	[Fact]
	public void SayOnlyTheGateFailedWhenItHasNoFindings()
	{
		var failedGate = FailedGate(Faker.Create<string>());

		var verdict = buildVerdict.Build(PipelineOf(true, failedGate));

		verdict.Message.Should().Be($"{failedGate.GateName} failed.");
	}

	[Fact]
	public void ListEveryGateInRunOrder()
	{
		GateResult[] gateResults =
		[
			GateWithStatus(GateStatus.Passed),
			GateWithStatus(GateStatus.Failed),
			GateWithStatus(GateStatus.Skipped),
		];

		var verdict = buildVerdict.Build(PipelineOf(true, gateResults));

		verdict.Gates.Select(gate => gate.Name).Should().Equal(gateResults.Select(gateResult => gateResult.GateName));
	}

	[Fact]
	public void CarryTheGateStage()
	{
		var gateResult = GateWithStatus(GateStatus.Passed);

		gateResult.Stage = GateStage.Standards;

		var verdict = buildVerdict.Build(PipelineOf(false, gateResult));

		verdict.Gates[0].Stage.Should().Be(GateStage.Standards);
	}

	[Fact]
	public void CarryTheGateStatus()
	{
		var verdict = buildVerdict.Build(PipelineOf(false, GateWithStatus(GateStatus.Warning)));

		verdict.Gates[0].Status.Should().Be(GateStatus.Warning);
	}

	[Fact]
	public void CarryTheGateFindings()
	{
		var gateResult = GateWithStatus(GateStatus.Warning);

		gateResult.Findings = [Faker.Create<GateFinding>(), Faker.Create<GateFinding>()];

		var verdict = buildVerdict.Build(PipelineOf(false, gateResult));

		verdict.Gates[0].Findings.Should().Equal(gateResult.Findings);
	}

	[Fact]
	public void CopyTheGateFindingListRatherThanShareIt()
	{
		var gateResult = GateWithStatus(GateStatus.Warning);
		var findingCountWhenBuilt = gateResult.Findings.Count;
		var verdict = buildVerdict.Build(PipelineOf(false, gateResult));

		gateResult.Findings.Add(Faker.Create<GateFinding>());

		verdict.Gates[0].Findings.Should().HaveCount(findingCountWhenBuilt);
	}

	[Fact]
	public void ConvertTheDurationToMilliseconds()
	{
		var gateResult = GateWithStatus(GateStatus.Passed);

		gateResult.Duration = TimeSpan.FromMilliseconds(1234);

		var verdict = buildVerdict.Build(PipelineOf(false, gateResult));

		verdict.Gates[0].DurationMilliseconds.Should().Be(1234);
	}

	[Fact]
	public void ReturnInvalidIntentForAnInvalidIntent()
	{
		var verdict = buildVerdict.BuildInvalidIntent(InvalidIntent());

		verdict.Status.Should().Be(VerdictStatus.InvalidIntent);
	}

	[Fact]
	public void UseTheIntentErrorMessageForAnInvalidIntent()
	{
		var intentResult = InvalidIntent();

		var verdict = buildVerdict.BuildInvalidIntent(intentResult);

		verdict.Message.Should().Be(intentResult.Message);
	}

	[Fact]
	public void ListNoGatesForAnInvalidIntent()
	{
		var verdict = buildVerdict.BuildInvalidIntent(InvalidIntent());

		verdict.Gates.Should().BeEmpty();
	}
}
