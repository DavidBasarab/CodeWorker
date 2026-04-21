namespace FatCat.CodeWorker.Settings;

public class ClaudeSettings
{
	public string Model { get; set; }

	public int MaxTurns { get; set; }

	public bool SkipPermissions { get; set; }

	public string OutputFormat { get; set; }

	public string SystemPromptFile { get; set; }

	public List<string> AllowedTools { get; set; }

	public int TimeoutMinutes { get; set; }

	public ClaudeSettings MergeWith(ClaudeSettings overrides)
	{
		if (overrides == null)
		{
			return new ClaudeSettings
			{
				Model = Model,
				MaxTurns = MaxTurns,
				SkipPermissions = SkipPermissions,
				OutputFormat = OutputFormat,
				SystemPromptFile = SystemPromptFile,
				AllowedTools = AllowedTools,
				TimeoutMinutes = TimeoutMinutes,
			};
		}

		return new ClaudeSettings
		{
			Model = string.IsNullOrEmpty(overrides.Model) ? Model : overrides.Model,
			MaxTurns = overrides.MaxTurns > 0 ? overrides.MaxTurns : MaxTurns,
			SkipPermissions = overrides.SkipPermissions,
			OutputFormat = string.IsNullOrEmpty(overrides.OutputFormat) ? OutputFormat : overrides.OutputFormat,
			SystemPromptFile = string.IsNullOrEmpty(overrides.SystemPromptFile) ? SystemPromptFile : overrides.SystemPromptFile,
			AllowedTools = overrides.AllowedTools is { Count: > 0 } ? overrides.AllowedTools : AllowedTools,
			TimeoutMinutes = overrides.TimeoutMinutes > 0 ? overrides.TimeoutMinutes : TimeoutMinutes,
		};
	}
}
