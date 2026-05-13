param(
	[Parameter(Mandatory = $true)] [string] $PromptFile,
	[Parameter(Mandatory = $true)] [string] $TranscriptFile,
	[Parameter(Mandatory = $true)] [string] $StderrFile,
	[Parameter(Mandatory = $true)] [string] $DoneSentinel,
	[Parameter(Mandatory = $true)] [string] $PidFile,
	[Parameter(Mandatory = $true)] [string] $WrapperStartedFile,
	[Parameter(Mandatory = $true)] [string] $WrapperLogFile,
	[Parameter(Mandatory = $false)] [string] $ClaudeArgsFile = ""
)

$ClaudeArgs = @()

if (-not [string]::IsNullOrEmpty($ClaudeArgsFile) -and (Test-Path -LiteralPath $ClaudeArgsFile))
{
	$ClaudeArgs = @([System.IO.File]::ReadAllLines($ClaudeArgsFile, [System.Text.Encoding]::UTF8))
}

$ErrorActionPreference = "Continue"

function Write-WrapperLog
{
	param([Parameter(Mandatory = $true)] [string] $Message)

	$line = "[{0:o}] {1}" -f (Get-Date), $Message

	try
	{
		[System.IO.File]::AppendAllText($WrapperLogFile, $line + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
	}
	catch
	{
	}
}

function Write-TranscriptLine
{
	param([Parameter(Mandatory = $true)] [string] $Line)

	[System.IO.File]::AppendAllText($TranscriptFile, $Line + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
}

try
{
	[System.IO.File]::WriteAllText($WrapperStartedFile, "$PID|$((Get-Date).ToString('o'))", [System.Text.Encoding]::UTF8)

	Write-WrapperLog -Message "wrapper started PID=$PID"
	Write-WrapperLog -Message "PromptFile=$PromptFile"
	Write-WrapperLog -Message "TranscriptFile=$TranscriptFile"
	Write-WrapperLog -Message "StderrFile=$StderrFile"
	Write-WrapperLog -Message "DoneSentinel=$DoneSentinel"
	Write-WrapperLog -Message "PidFile=$PidFile"
	Write-WrapperLog -Message "ClaudeArgs.Count=$($ClaudeArgs.Count)"

	for ($index = 0; $index -lt $ClaudeArgs.Count; $index++)
	{
		Write-WrapperLog -Message "ClaudeArgs[$index]=$($ClaudeArgs[$index])"
	}

	[System.IO.File]::WriteAllText($PidFile, "$PID", [System.Text.Encoding]::UTF8)

	$startedEvent = @{
		type      = "orchestrator-started"
		pid       = $PID
		startedAt = (Get-Date).ToString("o")
	} | ConvertTo-Json -Compress

	Write-TranscriptLine -Line $startedEvent

	$claudeCommand = (Get-Command claude -ErrorAction SilentlyContinue)

	if ($null -eq $claudeCommand)
	{
		Write-WrapperLog -Message "ERROR: claude command not found on PATH"

		[System.IO.File]::AppendAllText($StderrFile, "claude command not found on PATH" + [Environment]::NewLine, [System.Text.Encoding]::UTF8)

		$exit = 127
	}
	else
	{
		Write-WrapperLog -Message "claude resolved to: $($claudeCommand.Source)"

		$argList = @("--output-format", "stream-json", "--verbose")
		$argList += $ClaudeArgs
		$argList += @("-p")

		Write-WrapperLog -Message "invoking claude with $($argList.Count) args, stdin redirected from PromptFile"

		$startProcessArgs = @{
			FilePath               = $claudeCommand.Source
			ArgumentList           = $argList
			RedirectStandardInput  = $PromptFile
			RedirectStandardOutput = $TranscriptFile
			RedirectStandardError  = $StderrFile
			NoNewWindow            = $true
			Wait                   = $true
			PassThru               = $true
		}

		$exit = -1

		try
		{
			$process = Start-Process @startProcessArgs
			$exit = $process.ExitCode
			Write-WrapperLog -Message "claude exited ExitCode=$exit"
		}
		catch
		{
			$exit = 1
			Write-WrapperLog -Message "Start-Process threw: $($_.Exception.Message)"

			[System.IO.File]::AppendAllText(
				$StderrFile,
				"orchestrator caught exception: $($_.Exception.Message)" + [Environment]::NewLine,
				[System.Text.Encoding]::UTF8
			)
		}
	}

	$doneEvent = @{
		type     = "orchestrator-done"
		exitCode = $exit
		endedAt  = (Get-Date).ToString("o")
	} | ConvertTo-Json -Compress

	Write-TranscriptLine -Line $doneEvent

	[System.IO.File]::WriteAllText($DoneSentinel, "$exit", [System.Text.Encoding]::UTF8)

	Write-WrapperLog -Message "wrapper done ExitCode=$exit"

	exit $exit
}
catch
{
	Write-WrapperLog -Message "wrapper outer catch: $($_.Exception.Message)"

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
