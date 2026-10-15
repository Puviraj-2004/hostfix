$ErrorActionPreference = 'Stop'

Write-Host "Installing hostfix CLI..." -ForegroundColor Cyan

$repo = "Puviraj-2004/hostfix"
$releaseUrl = "https://github.com/$repo/releases/latest/download/hostfix-win-x64.exe"

$installDir = "$env:LOCALAPPDATA\Programs\hostfix"
$exePath = "$installDir\hostfix.exe"

if (!(Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

Write-Host "Downloading latest binary from GitHub Releases..." -ForegroundColor Yellow
Invoke-WebRequest -Uri $releaseUrl -OutFile $exePath

# Add to user PATH safely (preserving REG_EXPAND_SZ and avoiding setx truncation)
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$installDir*") {
    Write-Host "Adding $installDir to User PATH..." -ForegroundColor Yellow
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$installDir", "User")
    $env:Path += ";$installDir"
}

Write-Host "hostfix v1.0.0 installed successfully!" -ForegroundColor Green
Write-Host "Open a new terminal and run: hostfix" -ForegroundColor Green