namespace FatCat.CodeWorker.Commands.Run;

public class RepositoryValidationResult
{
	public bool IsValid { get; set; }

	public List<string> Errors { get; set; } = new();
}
