using System.Text;
using FatCat.CodeWorker.Commands.Run;

namespace FatCat.CodeWorker.Claude;

public interface IBuildReferenceSystemPrompt
{
	string Build(List<ReferenceFile> referenceFiles);
}

public class BuildReferenceSystemPrompt : IBuildReferenceSystemPrompt
{
	public string Build(List<ReferenceFile> referenceFiles)
	{
		var contentBuilder = new StringBuilder();

		foreach (var file in referenceFiles)
		{
			contentBuilder.AppendLine($"## Reference: {file.Name}");
			contentBuilder.AppendLine(file.Content);
			contentBuilder.AppendLine();
		}

		return contentBuilder.ToString().Replace("\"", "\\\"").TrimEnd();
	}
}
