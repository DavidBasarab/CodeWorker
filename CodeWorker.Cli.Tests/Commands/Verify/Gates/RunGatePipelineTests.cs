using FatCat.CodeWorker.Cli.Commands.Verify.Gates;
using FatCat.CodeWorker.Cli.Time;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Gates;

public class RunGatePipelineTests
{
	private readonly List<IVerificationGate> gates = [];
	private readonly List<string> runOrder = [];
	private readonly List<GateResult> returnedResults = [];
	private readonly VerificationContext context = Faker.Create<VerificationContext>();
	private readonly IClock clock = A.Fake<IClock>();
	private readonly RunGatePipeline runGatePipeline;

	public RunGatePipelineTests()
	{
		A.CallTo(() => clock.UtcNow).Returns(Faker.Create<DateTime>());

		runGatePipeline = new RunGatePipeline(gates, clock);
	}

	private static TimeSpan RandomElapsedTime()
	{
		return TimeSpan.FromMilliseconds(Faker.RandomInt(1, 100_000));
	}

	private void ClockReads(params DateTime[] times)
	{
		A.CallTo(() => clock.UtcNow).ReturnsNextFromSequence(times);
	}

	private IVerificationGate AddGate(GateStage stage, GateStatus status)
	{
		var gate = A.Fake<IVerificationGate>();
		var name = Faker.Create<string>();
		var gateResult = new GateResult { Status = status, Findings = [Faker.Create<GateFinding>()] };

		A.CallTo(() => gate.Name).Returns(name);
		A.CallTo(() => gate.Stage).Returns(stage);
		A.CallTo(() => gate.Run(A<VerificationContext>._)).Invokes(() => runOrder.Add(name)).Returns(gateResult);

		gates.Add(gate);
		returnedResults.Add(gateResult);

		return gate;
	}

	[Fact]
	public async Task ReturnNotBlockedWhenThereAreNoGates()
	{
		var result = await runGatePipeline.Run(context);

		result.IsBlocked.Should().BeFalse();
	}

	[Fact]
	public async Task ReturnNoGateResultsWhenThereAreNoGates()
	{
		var result = await runGatePipeline.Run(context);

		result.GateResults.Should().BeEmpty();
	}

	[Fact]
	public async Task RunEveryGateWhenAllPass()
	{
		AddGate(GateStage.Compile, GateStatus.Passed);
		AddGate(GateStage.Test, GateStatus.Passed);
		AddGate(GateStage.Mutation, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result.GateResults.Should().HaveCount(3);
	}

	[Fact]
	public async Task ReturnNotBlockedWhenAllGatesPass()
	{
		AddGate(GateStage.Compile, GateStatus.Passed);
		AddGate(GateStage.Test, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result.IsBlocked.Should().BeFalse();
	}

	[Fact]
	public async Task PassTheContextToTheFirstGate()
	{
		var firstGate = AddGate(GateStage.Compile, GateStatus.Passed);
		AddGate(GateStage.Test, GateStatus.Passed);

		await runGatePipeline.Run(context);

		A.CallTo(() => firstGate.Run(context)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassTheContextToTheLastGate()
	{
		AddGate(GateStage.Compile, GateStatus.Passed);
		var lastGate = AddGate(GateStage.Test, GateStatus.Passed);

		await runGatePipeline.Run(context);

		A.CallTo(() => lastGate.Run(context)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RunGatesInStageOrder()
	{
		var intentGate = AddGate(GateStage.Intent, GateStatus.Passed);
		var compileGate = AddGate(GateStage.Compile, GateStatus.Passed);
		var mutationGate = AddGate(GateStage.Mutation, GateStatus.Passed);

		await runGatePipeline.Run(context);

		runOrder.Should().Equal([compileGate.Name, mutationGate.Name, intentGate.Name]);
	}

	[Fact]
	public async Task ReturnResultsInStageOrder()
	{
		AddGate(GateStage.Intent, GateStatus.Passed);
		AddGate(GateStage.Compile, GateStatus.Passed);
		AddGate(GateStage.Mutation, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result
			.GateResults.Select(gateResult => gateResult.Stage)
			.Should()
			.Equal([GateStage.Compile, GateStage.Mutation, GateStage.Intent]);
	}

	[Fact]
	public async Task KeepInjectedOrderWithinTheSameStage()
	{
		var firstStandardsGate = AddGate(GateStage.Standards, GateStatus.Passed);
		var secondStandardsGate = AddGate(GateStage.Standards, GateStatus.Passed);

		await runGatePipeline.Run(context);

		runOrder.Should().Equal([firstStandardsGate.Name, secondStandardsGate.Name]);
	}

	[Fact]
	public async Task StopRunningGatesAfterTheFirstFailure()
	{
		AddGate(GateStage.Compile, GateStatus.Failed);
		var testGate = AddGate(GateStage.Test, GateStatus.Passed);

		await runGatePipeline.Run(context);

		A.CallTo(() => testGate.Run(A<VerificationContext>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task MarkTheGatesAfterAFailureAsSkipped()
	{
		AddGate(GateStage.Compile, GateStatus.Failed);
		AddGate(GateStage.Test, GateStatus.Passed);
		AddGate(GateStage.Mutation, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result
			.GateResults.Select(gateResult => gateResult.Status)
			.Should()
			.Equal([GateStatus.Failed, GateStatus.Skipped, GateStatus.Skipped]);
	}

	[Fact]
	public async Task NameTheSkippedGatesOnTheirResults()
	{
		AddGate(GateStage.Compile, GateStatus.Failed);
		var skippedGate = AddGate(GateStage.Test, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result.GateResults[1].GateName.Should().Be(skippedGate.Name);
	}

	[Fact]
	public async Task StampTheGateStageOnTheSkippedResults()
	{
		AddGate(GateStage.Compile, GateStatus.Failed);
		AddGate(GateStage.Standards, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result.GateResults[1].Stage.Should().Be(GateStage.Standards);
	}

	[Fact]
	public async Task ReturnBlockedWhenAGateFails()
	{
		AddGate(GateStage.Compile, GateStatus.Passed);
		AddGate(GateStage.Test, GateStatus.Failed);

		var result = await runGatePipeline.Run(context);

		result.IsBlocked.Should().BeTrue();
	}

	[Fact]
	public async Task ContinuePastAWarning()
	{
		AddGate(GateStage.Compile, GateStatus.Warning);
		var testGate = AddGate(GateStage.Test, GateStatus.Passed);

		await runGatePipeline.Run(context);

		A.CallTo(() => testGate.Run(A<VerificationContext>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnNotBlockedWhenAGateWarns()
	{
		AddGate(GateStage.Compile, GateStatus.Warning);
		AddGate(GateStage.Test, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result.IsBlocked.Should().BeFalse();
	}

	[Fact]
	public async Task StampTheGateNameOnItsResult()
	{
		var gate = AddGate(GateStage.Compile, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result.GateResults[0].GateName.Should().Be(gate.Name);
	}

	[Fact]
	public async Task StampTheGateStageOnItsResult()
	{
		AddGate(GateStage.Standards, GateStatus.Passed);

		var result = await runGatePipeline.Run(context);

		result.GateResults[0].Stage.Should().Be(GateStage.Standards);
	}

	[Fact]
	public async Task KeepTheGateFindingsOnItsResult()
	{
		AddGate(GateStage.Compile, GateStatus.Passed);
		var returnedFindings = returnedResults[0].Findings.ToList();

		var result = await runGatePipeline.Run(context);

		result.GateResults[0].Findings.Should().Equal(returnedFindings);
	}

	[Fact]
	public async Task StampTheGateDurationOnItsResult()
	{
		AddGate(GateStage.Compile, GateStatus.Passed);
		var start = Faker.Create<DateTime>();
		var elapsed = RandomElapsedTime();

		ClockReads(start, start + elapsed);

		var result = await runGatePipeline.Run(context);

		result.GateResults[0].Duration.Should().Be(elapsed);
	}

	[Fact]
	public async Task TimeEachGateSeparately()
	{
		AddGate(GateStage.Compile, GateStatus.Passed);
		AddGate(GateStage.Test, GateStatus.Passed);
		var firstStart = Faker.Create<DateTime>();
		var secondStart = Faker.Create<DateTime>();
		var secondElapsed = RandomElapsedTime();

		ClockReads(firstStart, firstStart + RandomElapsedTime(), secondStart, secondStart + secondElapsed);

		var result = await runGatePipeline.Run(context);

		result.GateResults[1].Duration.Should().Be(secondElapsed);
	}

	[Fact]
	public async Task ReportZeroDurationForASkippedGate()
	{
		AddGate(GateStage.Compile, GateStatus.Failed);
		AddGate(GateStage.Test, GateStatus.Passed);
		var start = Faker.Create<DateTime>();

		ClockReads(start, start + RandomElapsedTime(), Faker.Create<DateTime>(), Faker.Create<DateTime>());

		var result = await runGatePipeline.Run(context);

		result.GateResults[1].Duration.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public async Task NotReadTheClockForASkippedGate()
	{
		AddGate(GateStage.Compile, GateStatus.Failed);
		AddGate(GateStage.Test, GateStatus.Passed);

		await runGatePipeline.Run(context);

		A.CallTo(() => clock.UtcNow).MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task TimeAFailingGate()
	{
		AddGate(GateStage.Compile, GateStatus.Failed);
		var start = Faker.Create<DateTime>();
		var elapsed = RandomElapsedTime();

		ClockReads(start, start + elapsed);

		var result = await runGatePipeline.Run(context);

		result.GateResults[0].Duration.Should().Be(elapsed);
	}
}
