using FatCat.CodeWorker.Claude;
using Serilog;

namespace FatCat.CodeWorker;

public class CodeWorkerApplication(IRunClaude runClaude, ILogger logger)
{
	public async Task DoWork(string[] args)
	{
		logger.Information("Welcome to Code Worker");

		if (args.Length == 0)
		{
			logger.Error("No markdown file path provided. Usage: CodeWorker <path-to-markdown-file>");

			return;
		}

		var markdownFilePath = args[0];

		await runClaude.Run(markdownFilePath);
	}
}
