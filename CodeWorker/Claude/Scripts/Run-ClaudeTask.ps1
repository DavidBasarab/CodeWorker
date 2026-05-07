param(
	[Parameter(Mandatory = $true)] [string] $PromptFile,
	[Parameter(Mandatory = $true)] [string] $TranscriptFile,
	[Parameter(Mandatory = $true)] [string] $StderrFile,
	[Parameter(Mandatory = $true)] [string] $DoneSentinel,
	[Parameter(Mandatory = $true)] [string] $PidFile,
	[Parameter(Mandatory = $false)] [string[]] $ClaudeArgs = @()
)

$ErrorActionPreference = "Continue"

function Write-TranscriptLine
{
	param([Parameter(Mandatory = $true)] [string] $Line)

	[System.IO.File]::AppendAllText($TranscriptFile, $Line + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
}

try
{
	[System.IO.File]::WriteAllText($PidFile, "$PID", [System.Text.Encoding]::UTF8)

	$startedEvent = @{
		type    = "orchestrator-started"
		pid     = $PID
		startedAt = (Get-Date).ToString("o")
	} | ConvertTo-Json -Compress

	Write-TranscriptLine -Line $startedEvent

	$promptText = [System.IO.File]::ReadAllText($PromptFile)

	$argList = @("--output-format", "stream-json", "--verbose")
	$argList += $ClaudeArgs
	$argList += @("-p")

	$exit = -1

	try
	{
		$promptText | & claude @argList 1>> $TranscriptFile 2>> $StderrFile
		$exit = $LASTEXITCODE
	}
	catch
	{
		$exit = 1

		[System.IO.File]::AppendAllText(
			$StderrFile,
			"orchestrator caught exception: $($_.Exception.Message)" + [Environment]::NewLine,
			[System.Text.Encoding]::UTF8
		)
	}

	$doneEvent = @{
		type     = "orchestrator-done"
		exitCode = $exit
		endedAt  = (Get-Date).ToString("o")
	} | ConvertTo-Json -Compress

	Write-TranscriptLine -Line $doneEvent

	[System.IO.File]::WriteAllText($DoneSentinel, "$exit", [System.Text.Encoding]::UTF8)

	exit $exit
}
catch
{
	$failureEvent = @{
		type     = "orchestrator-done"
		exitCode = 1
		error    = $_.Exception.Message
		endedAt  = (Get-Date).ToString("o")
	} | ConvertTo-Json -Compress

	try
	{
		Write-TranscriptLine -Line $failureEvent
	}
	catch
	{
	}

	try
	{
		[System.IO.File]::WriteAllText($DoneSentinel, "1", [System.Text.Encoding]::UTF8)
	}
	catch
	{
	}

	exit 1
}
