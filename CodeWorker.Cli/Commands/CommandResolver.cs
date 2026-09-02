using FatCat.CodeWorker.Cli.Commands.Verify;

namespace FatCat.CodeWorker.Cli.Commands;

public interface IResolveCommand
{
	ICommand Resolve(string[] args);
}

public class CommandResolver(IRunVerifyCommand verifyCommand) : IResolveCommand
{
	public ICommand Resolve(string[] args)
	{
		if (args.Length == 0)
		{
			return verifyCommand;
		}

		return args[0].ToLowerInvariant() switch
		{
			"verify" => verifyCommand,
			_ => verifyCommand,
		};
	}
}
