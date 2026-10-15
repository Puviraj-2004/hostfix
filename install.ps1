# Strict mode: fail on error
$ErrorActionPreference = 'Stop'

Write-Host "Installing hostfix CLI..." -ForegroundColor Cyan

$repo = "Puviraj-2004/hostfix"
$version = "v1.0.0"
$binaryName = "hostfix-win-x64.exe"
$releaseUrl = "https://github.com/$repo/releases/download/$version/$binaryName"
$checksumUrl = "https://github.com/$repo/releases/download/$version/$binaryName.sha256"

$installDir = "$env:LOCALAPPDATA\Programs\hostfix"
$targetExe = "$installDir\hostfix.exe"
$tempExe = "$installDir\temp-$binaryName"
$tempChecksum = "$installDir\temp-$binaryName.sha256"

if (!(Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

try {
    Write-Host "Downloading $binaryName ($version)..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $releaseUrl -OutFile $tempExe -UseBasicParsing
    Invoke-WebRequest -Uri $checksumUrl -OutFile $tempChecksum -UseBasicParsing

    # 1. Cryptographic Verification
    Write-Host "Verifying SHA-256 checksum..." -ForegroundColor Yellow
    $expectedHash = (Get-Content $tempChecksum).Trim().Split(" ")[0].ToLower()
    $actualHash = (Get-FileHash -Path $tempExe -Algorithm SHA256).Hash.ToLower()

    if ($expectedHash -ne $actualHash) {
        throw "Checksum verification failed! Expected: $expectedHash, Got: $actualHash. Aborting."
    }

    Move-Item -Path $tempExe -Destination $targetExe -Force
    Remove-Item -Path $tempChecksum -Force -ErrorAction SilentlyContinue

    # 2. Registry Safe PATH Mutation (Never use SetEnvironmentVariable; preserve REG_EXPAND_SZ)
    $regKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Environment", $true)
    if ($regKey -eq $null) {
        throw "Cannot open HKCU\Environment registry subkey for writing."
    }

    $rawPath = $regKey.GetValue("Path", $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    $valKind = $regKey.GetValueKind("Path")

    if ($rawPath -eq $null) {
        $rawPath = ""
    }

    $entries = $rawPath.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)
    $alreadyExists = $false
    foreach ($entry in $entries) {
        if ($entry.Trim().TrimEnd('\', '/') -ieq $installDir.TrimEnd('\', '/')) {
            $alreadyExists = $true
            break
        }
    }

    if (-not $alreadyExists) {
        Write-Host "Adding $installDir to User PATH preserving registry type ($valKind)..." -ForegroundColor Yellow
        $newPath = if ([string]::IsNullOrWhiteSpace($rawPath)) { $installDir } else { "$rawPath;$installDir" }
        $regKey.SetValue("Path", $newPath, $valKind)
        
        # Broadcast WM_SETTINGCHANGE
        $HWND_BROADCAST = [System.IntPtr]0xffff
        $WM_SETTINGCHANGE = 0x001A
        $result = [System.UIntPtr]::Zero
        Add-Type -Namespace Win32 -Name NativeMethods -MemberDefinition @'
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
            public static extern System.IntPtr SendMessageTimeout(
                System.IntPtr hWnd, uint Msg, System.UIntPtr wParam, string lParam,
                uint fuFlags, uint uTimeout, out System.UIntPtr lpdwResult);
'@ -ErrorAction SilentlyContinue
        [Win32.NativeMethods]::SendMessageTimeout($HWND_BROADCAST, $WM_SETTINGCHANGE, [System.UIntPtr]::Zero, "Environment", 2, 2000, [ref]$result) | Out-Null
    }

    $regKey.Close()

    Write-Host "hostfix installed successfully!" -ForegroundColor Green
    Write-Host "Restart your terminal and run: hostfix" -ForegroundColor Green
}
finally {
    Remove-Item -Path $tempExe -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $tempChecksum -Force -ErrorAction SilentlyContinue
}