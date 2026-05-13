using FatCat.CodeWorker.Commands.Help;
using FatCat.CodeWorker.Commands.Info;
using FatCat.CodeWorker.Commands.List;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Setup;
using FatCat.CodeWorker.Commands.Track;
using FatCat.CodeWorker.Commands.Untrack;

namespace FatCat.CodeWorker.Commands;

public interface IResolveCommand
{
	ICommand Resolve(string[] args);
}

public class CommandResolver(
	IRunSetupCommand setupCommand,
	IRunTaskCommand runTaskCommand,
	IRunSingleTaskCommand runSingleTaskCommand,
	IRunTrackCommand trackCommand,
	IRunUntrackCommand untrackCommand,
	IRunListCommand listCommand,
	IRunInfoCommand infoCommand,
	IRunHelpCommand helpCommand
) : IResolveCommand
{
	public ICommand Resolve(string[] args)
	{
		if (args.Length == 0)
		{
			return runTaskCommand;
		}

		return args[0].ToLowerInvariant() switch
		{
			"setup" => setupCommand,
			"track" => trackCommand,
			"untrack" => untrackCommand,
			"list" => listCommand,
			"info" => infoCommand,
			"run-task" => runSingleTaskCommand,
			"help" => helpCommand,
			"--help" => helpCommand,
			"-h" => helpCommand,
			"/?" => helpCommand,
			_ => runTaskCommand,
		};
	}
}
