using FatCat.CodeWorker.Process;
using Serilog;

namespace FatCat.CodeWorker.Claude;

public class ClaudeRunner(IRunProcess runProcess, ILogger logger) : IRunClaude
{
	public async Task<ProcessResult> Run(string markdownFilePath)
	{
		logger.Information("Starting Claude with markdown file {MarkdownFilePath}", markdownFilePath);

		var settings = new ProcessSettings { FileName = "claude", Arguments = $"-p --input-file \"{markdownFilePath}\"" };

		var result = await runProcess.Run(settings);

		if (result.ExitCode != 0)
		{
			logger.Warning("Claude exited with non-zero exit code {ExitCode}", result.ExitCode);
		}

		logger.Information("Claude exited with code {ExitCode}", result.ExitCode);

		return result;
	}
}
