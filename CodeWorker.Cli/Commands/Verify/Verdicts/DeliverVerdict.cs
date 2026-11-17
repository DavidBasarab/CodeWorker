using FatCat.CodeWorker.Cli.Process;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Verdicts;

public interface IDeliverVerdict
{
	Task Deliver(Verdict verdict, string verdictPath);
}

public class DeliverVerdict(
	ISerializeVerdict serializeVerdict,
	IFormatVerdictReport formatVerdictReport,
	IFileSystemTools fileSystemTools,
	ISetExitCode setExitCode,
	ILogger logger
) : IDeliverVerdict
{
	public async Task Deliver(Verdict verdict, string verdictPath)
	{
		if (!string.IsNullOrWhiteSpace(verdictPath))
		{
			await fileSystemTools.WriteAllText(Path.GetFullPath(verdictPath), serializeVerdict.Serialize(verdict));
		}

		logger.Information("{Report:l}", formatVerdictReport.Format(verdict));

		setExitCode.Set(ExitCodeFor(verdict.Status));
	}

	private static CliExitCode ExitCodeFor(VerdictStatus status)
	{
		return status switch
		{
			VerdictStatus.Green => CliExitCode.Success,
			VerdictStatus.Blocked => CliExitCode.Blocked,
			VerdictStatus.InvalidIntent => CliExitCode.InvalidInput,
			_ => throw new ArgumentOutOfRangeException(nameof(status)),
		};
	}
}
