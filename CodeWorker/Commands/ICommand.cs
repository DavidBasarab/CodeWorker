namespace FatCat.CodeWorker.Commands;

public interface ICommand
{
	Task Execute(string[] args);
}
