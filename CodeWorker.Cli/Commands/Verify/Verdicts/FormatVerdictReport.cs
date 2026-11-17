using FatCat.CodeWorker.Cli.Commands.Verify.Gates;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public interface IFormatVerdictReport
{
	string Format(Verdict verdict);
}

public class FormatVerdictReport : IFormatVerdictReport
{
	public string Format(Verdict verdict)
	{
		IEnumerable<string> lines = [StatusLine(verdict), .. verdict.Gates.SelectMany(GateLines)];

		return string.Join(Environment.NewLine, lines);
	}

	private static string StatusLine(Verdict verdict)
	{
		return $"Verdict: {StatusLabel(verdict.Status)} — {OnOneLine(verdict.Message)}";
	}

	private static string StatusLabel(VerdictStatus status)
	{
		return status switch
		{
			VerdictStatus.Green => "GREEN",
			VerdictStatus.Blocked => "BLOCKED",
			VerdictStatus.InvalidIntent => "INVALID INTENT",
			_ => throw new ArgumentOutOfRangeException(nameof(status)),
		};
	}

	private static IEnumerable<string> GateLines(GateVerdict gate)
	{
		return [GateLine(gate), .. gate.Findings.Select(FindingLine)];
	}

	private static string GateLine(GateVerdict gate)
	{
		return $"  [{gate.Status}] {gate.Name} ({gate.Stage}) {gate.DurationMilliseconds} ms";
	}

	private static string FindingLine(GateFinding finding)
	{
		var reason = OnOneLine(finding.Reason);

		if (finding.FilePath == null)
		{
			return $"    {reason}";
		}

		var filePath = OnOneLine(finding.FilePath);

		if (finding.Line == null)
		{
			return $"    {filePath} — {reason}";
		}

		return $"    {filePath}:{finding.Line} — {reason}";
	}

	private static string OnOneLine(string text)
	{
		return text.ReplaceLineEndings(" ");
	}
}
