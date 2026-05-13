namespace FatCat.CodeWorker.Commands;

public static class RequiredTaskFolders
{
	public static readonly IReadOnlyList<string> All = new[]
	{
		"todo",
		"pending",
		"done",
		"blocked",
		"failed",
		"reference",
		"logs",
	};
}
