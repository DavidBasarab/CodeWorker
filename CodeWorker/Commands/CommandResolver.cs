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
	IRunTrackCommand trackCommand,
	IRunUntrackCommand untrackCommand,
	IRunListCommand listCommand,
	IRunInfoCommand infoCommand
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
			_ => runTaskCommand,
		};
	}
}
