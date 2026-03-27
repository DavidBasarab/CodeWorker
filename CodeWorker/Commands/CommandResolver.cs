using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Commands.Setup;

namespace FatCat.CodeWorker.Commands;

public interface IResolveCommand
{
	ICommand Resolve(string[] args);
}

public class CommandResolver(IRunSetupCommand setupCommand, IRunTaskCommand runTaskCommand) : IResolveCommand
{
	public ICommand Resolve(string[] args)
	{
		var commandName = args[0];

		if (string.Equals(commandName, "setup", StringComparison.OrdinalIgnoreCase))
		{
			return setupCommand;
		}

		return runTaskCommand;
	}
}
