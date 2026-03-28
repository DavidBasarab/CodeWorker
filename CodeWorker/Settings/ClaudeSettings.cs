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
}
