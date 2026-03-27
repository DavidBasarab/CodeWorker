using FatCat.CodeWorker.Claude;
using Serilog;

namespace FatCat.CodeWorker.Commands.Run;

public interface IRunTaskCommand : ICommand { }

public class RunCommand(IRunClaude runClaude, ILogger logger) : IRunTaskCommand
{
	public async Task Execute(string[] args)
	{
		var markdownFilePath = args[0];

		logger.Information("Running task from {MarkdownFilePath}", markdownFilePath);

		await runClaude.Run(markdownFilePath);
	}
}
