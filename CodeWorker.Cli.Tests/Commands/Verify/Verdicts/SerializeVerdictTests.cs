using System.Text.Json;
using FatCat.CodeWorker.Cli.Commands.Verify.Gates;
using FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

namespace Testing.FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public class SerializeVerdictTests
{
	private readonly GateFinding finding;
	private readonly GateVerdict gate;
	private readonly SerializeVerdict serializeVerdict;
	private readonly Verdict verdict;

	public SerializeVerdictTests()
	{
		finding = Faker.Create<GateFinding>();
		finding.Line = Faker.RandomInt(1, 1_000);

		gate = Faker.Create<GateVerdict>();
		gate.Stage = GateStage.Compile;
		gate.Status = GateStatus.Failed;
		gate.DurationMilliseconds = Faker.RandomInt(1, 100_000);
		gate.Findings = [finding];

		verdict = Faker.Create<Verdict>();
		verdict.Status = VerdictStatus.Blocked;
		verdict.Gates = [gate];

		serializeVerdict = new SerializeVerdict();
	}

	[Fact]
	public void WriteTheStatusAsACamelCaseString()
	{
		var root = SerializeAndParse();

		root.GetProperty("status").GetString().Should().Be("blocked");
	}

	[Fact]
	public void WriteInvalidIntentAsCamelCase()
	{
		verdict.Status = VerdictStatus.InvalidIntent;

		var root = SerializeAndParse();

		root.GetProperty("status").GetString().Should().Be("invalidIntent");
	}

	[Fact]
	public void WriteTheMessage()
	{
		var root = SerializeAndParse();

		root.GetProperty("message").GetString().Should().Be(verdict.Message);
	}

	[Fact]
	public void WriteEachGateName()
	{
		var secondGate = Faker.Create<GateVerdict>();

		verdict.Gates = [gate, secondGate];

		var gateNames = SerializeAndParse()
			.GetProperty("gates")
			.EnumerateArray()
			.Select(gateElement => gateElement.GetProperty("name").GetString())
			.ToList();

		gateNames.Should().Equal(verdict.Gates.Select(gateVerdict => gateVerdict.Name));
	}

	[Fact]
	public void WriteTheGateStageAsACamelCaseString()
	{
		var gateElement = FirstGate();

		gateElement.GetProperty("stage").GetString().Should().Be("compile");
	}

	[Fact]
	public void WriteTheGateStatusAsACamelCaseString()
	{
		var gateElement = FirstGate();

		gateElement.GetProperty("status").GetString().Should().Be("failed");
	}

	[Fact]
	public void WriteTheDurationInMilliseconds()
	{
		var gateElement = FirstGate();

		gateElement.GetProperty("durationMilliseconds").GetInt64().Should().Be(gate.DurationMilliseconds);
	}

	[Fact]
	public void WriteTheFindingFilePath()
	{
		var findingElement = FirstFinding();

		findingElement.GetProperty("filePath").GetString().Should().Be(finding.FilePath);
	}

	[Fact]
	public void WriteTheFindingLine()
	{
		var findingElement = FirstFinding();

		findingElement.GetProperty("line").GetInt32().Should().Be(finding.Line.Value);
	}

	[Fact]
	public void WriteTheFindingReason()
	{
		var findingElement = FirstFinding();

		findingElement.GetProperty("reason").GetString().Should().Be(finding.Reason);
	}

	[Fact]
	public void OmitANullFindingLine()
	{
		finding.Line = null;

		var findingElement = FirstFinding();

		findingElement.TryGetProperty("line", out _).Should().BeFalse();
	}

	[Fact]
	public void UseCamelCasePropertyNames()
	{
		var root = SerializeAndParse();

		root.TryGetProperty("gates", out _).Should().BeTrue();
	}

	[Fact]
	public void WriteAnEmptyFindingsListAsAnEmptyArray()
	{
		gate.Findings = [];

		var gateElement = FirstGate();

		gateElement.GetProperty("findings").GetArrayLength().Should().Be(0);
	}

	[Theory]
	[InlineData(VerdictStatus.Green, "green")]
	[InlineData(VerdictStatus.Blocked, "blocked")]
	[InlineData(VerdictStatus.InvalidIntent, "invalidIntent")]
	public void WriteEachVerdictStatusAsItsContractString(VerdictStatus status, string expectedStatus)
	{
		verdict.Status = status;

		var root = SerializeAndParse();

		root.GetProperty("status").GetString().Should().Be(expectedStatus);
	}

	[Theory]
	[InlineData(GateStage.Compile, "compile")]
	[InlineData(GateStage.Test, "test")]
	[InlineData(GateStage.Mutation, "mutation")]
	[InlineData(GateStage.Standards, "standards")]
	[InlineData(GateStage.Intent, "intent")]
	public void WriteEachGateStageAsItsContractString(GateStage stage, string expectedStage)
	{
		gate.Stage = stage;

		var gateElement = FirstGate();

		gateElement.GetProperty("stage").GetString().Should().Be(expectedStage);
	}

	[Theory]
	[InlineData(GateStatus.Passed, "passed")]
	[InlineData(GateStatus.Warning, "warning")]
	[InlineData(GateStatus.Failed, "failed")]
	[InlineData(GateStatus.Skipped, "skipped")]
	public void WriteEachGateStatusAsItsContractString(GateStatus status, string expectedStatus)
	{
		gate.Status = status;

		var gateElement = FirstGate();

		gateElement.GetProperty("status").GetString().Should().Be(expectedStatus);
	}

	private JsonElement SerializeAndParse()
	{
		var json = serializeVerdict.Serialize(verdict);

		using var document = JsonDocument.Parse(json);

		return document.RootElement.Clone();
	}

	private JsonElement FirstGate()
	{
		return SerializeAndParse().GetProperty("gates")[0];
	}

	private JsonElement FirstFinding()
	{
		return FirstGate().GetProperty("findings")[0];
	}
}
