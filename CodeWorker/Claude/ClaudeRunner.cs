using System.Text;
using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Process;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace FatCat.CodeWorker.Claude;

public interface IRunClaude
{
	Task<ProcessResult> Run(string markdownFilePath, ClaudeSettings claudeSettings, List<ReferenceFile> referenceFiles);
}

public class ClaudeRunner(IRunProcess runProcess, IFileSystemTools fileSystemTools, ILogger logger) : IRunClaude
{
	public async Task<ProcessResult> Run(
		string markdownFilePath,
		ClaudeSettings claudeSettings,
		List<ReferenceFile> referenceFiles
	)
	{
		logger.Information("Starting Claude with markdown file {MarkdownFilePath}", markdownFilePath);

		logger.Information(
			"Claude settings: Model={Model}, MaxTurns={MaxTurns}, SkipPermissions={SkipPermissions}, OutputFormat={OutputFormat}, TimeoutMinutes={TimeoutMinutes}",
			claudeSettings.Model,
			claudeSettings.MaxTurns,
			claudeSettings.SkipPermissions,
			claudeSettings.OutputFormat,
			claudeSettings.TimeoutMinutes
		);

		var promptContent = await fileSystemTools.ReadAllText(markdownFilePath);
		var arguments = BuildArguments(claudeSettings, referenceFiles);

		var settings = new ProcessSettings
		{
			FileName = "claude",
			Arguments = arguments,
			StandardInput = promptContent,
			TimeoutMilliseconds = claudeSettings.TimeoutMinutes > 0 ? claudeSettings.TimeoutMinutes * 60 * 1000 : 0,
		};

		var result = await runProcess.Run(settings);

		if (result.ExitCode != 0)
		{
			logger.Warning("Claude exited with non-zero exit code {ExitCode}", result.ExitCode);
		}

		logger.Information("Claude exited with code {ExitCode}", result.ExitCode);

		return result;
	}

	private string BuildReferenceContent(List<ReferenceFile> referenceFiles)
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

	private string BuildArguments(ClaudeSettings claudeSettings, List<ReferenceFile> referenceFiles)
	{
		var builder = new StringBuilder();

		builder.Append("-p");

		if (!string.IsNullOrEmpty(claudeSettings.Model))
		{
			builder.Append($" --model {claudeSettings.Model}");
		}

		if (claudeSettings.MaxTurns > 0)
		{
			builder.Append($" --max-turns {claudeSettings.MaxTurns}");
		}

		if (!string.IsNullOrEmpty(claudeSettings.OutputFormat))
		{
			builder.Append($" --output-format {claudeSettings.OutputFormat}");
		}

		if (!string.IsNullOrEmpty(claudeSettings.SystemPromptFile))
		{
			builder.Append($" --system-prompt \"{claudeSettings.SystemPromptFile}\"");
		}

		if (claudeSettings.SkipPermissions)
		{
			builder.Append(" --dangerously-skip-permissions");
		}

		if (claudeSettings.AllowedTools is { Count: > 0 })
		{
			foreach (var tool in claudeSettings.AllowedTools)
			{
				builder.Append($" --allowedTools \"{tool}\"");
			}
		}

		if (referenceFiles is { Count: > 0 })
		{
			var referenceContent = BuildReferenceContent(referenceFiles);

			builder.Append($" --append-system-prompt \"{referenceContent}\"");
		}

		return builder.ToString();
	}
}
