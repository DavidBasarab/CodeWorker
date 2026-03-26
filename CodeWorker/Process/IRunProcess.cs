namespace FatCat.CodeWorker.Process;

public interface IRunProcess
{
	public Task<ProcessResult> Run(ProcessSettings settings);
}
