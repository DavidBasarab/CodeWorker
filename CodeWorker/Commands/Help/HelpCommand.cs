using Serilog;

namespace FatCat.CodeWorker.Commands.Help;

public interface IRunHelpCommand : ICommand { }

public class HelpCommand(ILogger logger) : IRunHelpCommand
{
	public Task Execute(string[] args)
	{
		logger.Information("FatCatCodeWorker — overnight Claude task runner");
		logger.Information("");
		logger.Information("Usage: FatCatCodeWorker [command] [arguments]");
		logger.Information("");
		logger.Information("Commands:");
		logger.Information("  setup [path]");
		logger.Information("    Scaffold the tasks/ folder structure and tasks/settings.json in the");
		logger.Information("    target repository, then auto-track it. Defaults to the current directory.");
		logger.Information("");
		logger.Information("  track [path|name]");
		logger.Information("    Add a repository to the tracked list. With no argument, tracks the current");
		logger.Information("    directory. Accepts a folder name (matched against tracked repos) or an");
		logger.Information("    absolute path. Run setup first if the repository has no tasks/ folder.");
		logger.Information("");
		logger.Information("  untrack [path|name]");
		logger.Information("    Remove a repository from the tracked list. Files on disk are untouched —");
		logger.Information("    only the entry in appsettings.json is removed.");
		logger.Information("");
		logger.Information("  list");
		logger.Information("    Print every tracked repository with its enabled state.");
		logger.Information("");
		logger.Information("  info [count]");
		logger.Information("    Show the last N runs across all tracked repositories. Default is 10.");
		logger.Information("");
		logger.Information("  run-task <path>");
		logger.Information("    Run a single task file directly without walking the tracked queue.");
		logger.Information("");
		logger.Information("  help");
		logger.Information("    Show this help text.");
		logger.Information("");
		logger.Information("  (no arguments)");
		logger.Information("    Walk every enabled tracked repository and process its tasks/todo/ queue.");
		logger.Information("    This is what Windows Task Scheduler invokes overnight.");

		return Task.CompletedTask;
	}
}
