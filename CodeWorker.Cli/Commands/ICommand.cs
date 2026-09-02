namespace FatCat.CodeWorker.Cli.Commands;

public interface ICommand
{
	Task Execute(string[] args);
}
