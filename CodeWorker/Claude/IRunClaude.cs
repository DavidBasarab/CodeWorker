using FatCat.CodeWorker.Process;

namespace FatCat.CodeWorker.Claude;

public interface IRunClaude
{
	public Task<ProcessResult> Run(string markdownFilePath);
}
