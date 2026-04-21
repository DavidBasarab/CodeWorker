param(
	[string]$InstallPath = "C:\Tools\CodeWorker",
	[string]$RepoUrl = "https://github.com/DavidBasarab/CodeWorker.git",
	[string]$Branch = "main",
	[switch]$CreateScheduledTask,
	[string]$TaskName = "FatCatCodeWorker",
	[string]$ScheduleStartTime = "20:00",
	[string]$ScheduleEndTime = "07:00",
	[int]$ScheduleIntervalMinutes = 30,
	[string[]]$ScheduleDaysOfWeek = @(),
	[string]$ScheduleTaskArguments = ""
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message)
{
	Write-Host ""
	Write-Host "--- $Message ---" -ForegroundColor Cyan
}

function Test-CodeWorkerInstalled([string]$installPath)
{
	$exePath = Join-Path $installPath "FatCatCodeWorker.exe"

	return Test-Path $exePath
}

function Stop-RunningCodeWorker
{
	$runningProcesses = Get-Process -Name "FatCatCodeWorker" -ErrorAction SilentlyContinue

	if ($runningProcesses)
	{
		Write-Host "Stopping running FatCatCodeWorker process(es)..." -ForegroundColor Yellow

		$runningProcesses | Stop-Process -Force
		Start-Sleep -Seconds 1
	}
}

function Get-ScheduleDuration([string]$startTime, [string]$endTime)
{
	$start = [TimeSpan]::Parse($startTime)
	$end = [TimeSpan]::Parse($endTime)

	if ($end -le $start)
	{
		return [TimeSpan]::FromHours(24) - $start + $end
	}

	return $end - $start
}

function New-CodeWorkerScheduledTask
{
	param(
		[string]$name,
		[string]$exePath,
		[string]$startTime,
		[string]$endTime,
		[int]$intervalMinutes,
		[string[]]$daysOfWeek,
		[string]$taskArguments
	)

	$duration = Get-ScheduleDuration -startTime $startTime -endTime $endTime
	$startDateTime = [DateTime]::Today.Add([TimeSpan]::Parse($startTime))
	$repetitionInterval = New-TimeSpan -Minutes $intervalMinutes

	if ($daysOfWeek -and $daysOfWeek.Count -gt 0)
	{
		$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek $daysOfWeek -At $startDateTime
	}
	else
	{
		$trigger = New-ScheduledTaskTrigger -Daily -At $startDateTime
	}

	$repetitionTrigger = New-ScheduledTaskTrigger -Once -At $startDateTime `
		-RepetitionInterval $repetitionInterval `
		-RepetitionDuration $duration

	$trigger.Repetition = $repetitionTrigger.Repetition

	$workingDirectory = Split-Path $exePath -Parent

	if ([string]::IsNullOrWhiteSpace($taskArguments))
	{
		$action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $workingDirectory
	}
	else
	{
		$action = New-ScheduledTaskAction -Execute $exePath -Argument $taskArguments -WorkingDirectory $workingDirectory
	}

	$settings = New-ScheduledTaskSettingsSet `
		-MultipleInstances IgnoreNew `
		-AllowStartIfOnBatteries `
		-DontStopIfGoingOnBatteries `
		-StartWhenAvailable

	$existingTask = Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue

	if ($existingTask)
	{
		Write-Host "Replacing existing scheduled task '$name'..." -ForegroundColor Yellow

		Unregister-ScheduledTask -TaskName $name -Confirm:$false
	}

	Register-ScheduledTask -TaskName $name `
		-Trigger $trigger `
		-Action $action `
		-Settings $settings `
		-User $env:USERNAME `
		-RunLevel Limited | Out-Null

	Write-Host "Scheduled task '$name' registered." -ForegroundColor Green
	Write-Host "  Window:   $startTime - $endTime" -ForegroundColor Cyan
	Write-Host "  Interval: every $intervalMinutes minute(s)" -ForegroundColor Cyan
	Write-Host "  Policy:   skip if a previous run is still in progress" -ForegroundColor Cyan

	if ($daysOfWeek -and $daysOfWeek.Count -gt 0)
	{
		Write-Host "  Days:     $($daysOfWeek -join ', ')" -ForegroundColor Cyan
	}
	else
	{
		Write-Host "  Days:     Daily" -ForegroundColor Cyan
	}
}

# Detect install vs update
$isUpdate = Test-CodeWorkerInstalled -installPath $InstallPath

if ($isUpdate)
{
	Write-Step "Updating existing CodeWorker installation at $InstallPath"
}
else
{
	Write-Step "Installing CodeWorker to $InstallPath"
}

# Verify prerequisites
Write-Step "Checking prerequisites"

$missing = @()

if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue))
{
	$missing += "dotnet (.NET SDK) - https://dotnet.microsoft.com/download"
}

if (-not (Get-Command "git" -ErrorAction SilentlyContinue))
{
	$missing += "git - https://git-scm.com/downloads"
}

if (-not (Get-Command "claude" -ErrorAction SilentlyContinue))
{
	$missing += "claude (Claude Code CLI) - https://docs.anthropic.com/en/docs/claude-code"
}

if ($missing.Count -gt 0)
{
	Write-Host "Missing required tools:" -ForegroundColor Red

	foreach ($tool in $missing)
	{
		Write-Host "  - $tool" -ForegroundColor Red
	}

	exit 1
}

Write-Host "All prerequisites found." -ForegroundColor Green

# Clone or pull the repository
$tempRepo = Join-Path $env:TEMP "CodeWorker-build"

if (Test-Path $tempRepo)
{
	Write-Step "Fetching latest source in $tempRepo"

	Push-Location $tempRepo

	git fetch origin
	git checkout $Branch
	git pull origin $Branch

	Pop-Location
}
else
{
	Write-Step "Cloning repository to $tempRepo"

	git clone --branch $Branch $RepoUrl $tempRepo
}

# Stop any running instance so publish can overwrite the exe
if ($isUpdate)
{
	Stop-RunningCodeWorker
}

# Build and publish
if ($isUpdate)
{
	Write-Step "Republishing latest build to $InstallPath"
}
else
{
	Write-Step "Publishing to $InstallPath"
}

dotnet publish "$tempRepo\CodeWorker\CodeWorker.csproj" -c Release -o $InstallPath

if ($LASTEXITCODE -ne 0)
{
	Write-Host "Build failed." -ForegroundColor Red
	exit 1
}

if ($isUpdate)
{
	Write-Host "Updated successfully at $InstallPath" -ForegroundColor Green
}
else
{
	Write-Host "Published successfully to $InstallPath" -ForegroundColor Green
}

# Add to PATH if not already present
Write-Step "Checking PATH"

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")

if ($userPath -split ";" | Where-Object { $_ -eq $InstallPath })
{
	Write-Host "$InstallPath is already on PATH." -ForegroundColor Green
}
else
{
	[Environment]::SetEnvironmentVariable("Path", "$userPath;$InstallPath", "User")

	Write-Host "Added $InstallPath to user PATH." -ForegroundColor Green
	Write-Host "Open a new terminal for the change to take effect." -ForegroundColor Yellow
}

# Verify
Write-Step "Verifying installation"

$exePath = Join-Path $InstallPath "FatCatCodeWorker.exe"

if (Test-Path $exePath)
{
	Write-Host "FatCatCodeWorker.exe found at $exePath" -ForegroundColor Green
}
else
{
	Write-Host "FatCatCodeWorker.exe not found at $exePath" -ForegroundColor Red
	exit 1
}

if ($CreateScheduledTask)
{
	Write-Step "Registering scheduled task"

	New-CodeWorkerScheduledTask `
		-name $TaskName `
		-exePath $exePath `
		-startTime $ScheduleStartTime `
		-endTime $ScheduleEndTime `
		-intervalMinutes $ScheduleIntervalMinutes `
		-daysOfWeek $ScheduleDaysOfWeek `
		-taskArguments $ScheduleTaskArguments
}

Write-Host ""

if ($isUpdate)
{
	Write-Host "Update complete." -ForegroundColor Green
}
else
{
	Write-Host "Installation complete." -ForegroundColor Green
}

Write-Host "Run: FatCatCodeWorker --help" -ForegroundColor Cyan
