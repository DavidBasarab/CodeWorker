namespace FatCat.CodeWorker.Cli.Commands;

public interface IProcessArguments
{
	Task Process(string[] args);
}

public class ProcessArguments(IResolveCommand resolveCommand) : IProcessArguments
{
	public async Task Process(string[] args)
	{
		var command = resolveCommand.Resolve(args);

		await command.Execute(args);
	}
}
