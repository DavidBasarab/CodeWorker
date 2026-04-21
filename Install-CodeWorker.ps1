param(
	[string]$InstallPath = "C:\Tools\CodeWorker",
	[string]$RepoUrl = "https://github.com/DavidBasarab/CodeWorker.git",
	[string]$Branch = "main"
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message)
{
	Write-Host ""
	Write-Host "--- $Message ---" -ForegroundColor Cyan
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
	Write-Step "Updating existing source in $tempRepo"

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

# Build and publish
Write-Step "Publishing to $InstallPath"

dotnet publish "$tempRepo\CodeWorker\CodeWorker.csproj" -c Release -o $InstallPath

if ($LASTEXITCODE -ne 0)
{
	Write-Host "Build failed." -ForegroundColor Red
	exit 1
}

Write-Host "Published successfully to $InstallPath" -ForegroundColor Green

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

Write-Host ""
Write-Host "Installation complete." -ForegroundColor Green
Write-Host "Open a new terminal and run: FatCatCodeWorker --help" -ForegroundColor Cyan
