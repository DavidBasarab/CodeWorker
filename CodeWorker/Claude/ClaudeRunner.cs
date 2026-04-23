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

public class ClaudeRunner(
	IRunProcess runProcess,
	IFileSystemTools fileSystemTools,
	IBuildReferenceSystemPrompt buildReferenceSystemPrompt,
	ILogger logger
) : IRunClaude
{
	private StringBuilder arguments;

	public async Task<ProcessResult> Run(
		string markdownFilePath,
		ClaudeSettings claudeSettings,
		List<ReferenceFile> referenceFiles
	)
	{
		LogClaudeStartup(markdownFilePath, claudeSettings);

		var promptContent = await fileSystemTools.ReadAllText(markdownFilePath);
		var argumentString = BuildArguments(claudeSettings, referenceFiles);

		var settings = new ProcessSettings
		{
			FileName = "claude",
			Arguments = argumentString,
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

	private void LogClaudeStartup(string markdownFilePath, ClaudeSettings claudeSettings)
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
	}

	private string BuildArguments(ClaudeSettings claudeSettings, List<ReferenceFile> referenceFiles)
	{
		arguments = new StringBuilder();

		AppendPrintFlag();
		AppendModel(claudeSettings);
		AppendMaxTurns(claudeSettings);
		AppendOutputFormat(claudeSettings);
		AppendSystemPromptFile(claudeSettings);
		AppendSkipPermissions(claudeSettings);
		AppendAllowedTools(claudeSettings);
		AppendReferenceFiles(referenceFiles);

		return arguments.ToString();
	}

	private void AppendPrintFlag()
	{
		arguments.Append("-p");
	}

	private void AppendModel(ClaudeSettings claudeSettings)
	{
		if (!string.IsNullOrEmpty(claudeSettings.Model))
		{
			arguments.Append($" --model {claudeSettings.Model}");
		}
	}

	private void AppendMaxTurns(ClaudeSettings claudeSettings)
	{
		if (claudeSettings.MaxTurns > 0)
		{
			arguments.Append($" --max-turns {claudeSettings.MaxTurns}");
		}
	}

	private void AppendOutputFormat(ClaudeSettings claudeSettings)
	{
		if (!string.IsNullOrEmpty(claudeSettings.OutputFormat))
		{
			arguments.Append($" --output-format {claudeSettings.OutputFormat}");
		}
	}

	private void AppendSystemPromptFile(ClaudeSettings claudeSettings)
	{
		if (!string.IsNullOrEmpty(claudeSettings.SystemPromptFile))
		{
			arguments.Append($" --system-prompt \"{claudeSettings.SystemPromptFile}\"");
		}
	}

	private void AppendSkipPermissions(ClaudeSettings claudeSettings)
	{
		if (claudeSettings.SkipPermissions)
		{
			arguments.Append(" --dangerously-skip-permissions");
		}
	}

	private void AppendAllowedTools(ClaudeSettings claudeSettings)
	{
		if (claudeSettings.AllowedTools is { Count: > 0 })
		{
			foreach (var tool in claudeSettings.AllowedTools)
			{
				arguments.Append($" --allowedTools \"{tool}\"");
			}
		}
	}

	private void AppendReferenceFiles(List<ReferenceFile> referenceFiles)
	{
		if (referenceFiles is { Count: > 0 })
		{
			var referenceContent = buildReferenceSystemPrompt.Build(referenceFiles);

			arguments.Append($" --append-system-prompt \"{referenceContent}\"");
		}
	}
}
