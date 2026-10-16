# hostfix

[![CI](https://github.com/Puviraj-2004/hostfix/actions/workflows/ci.yml/badge.svg)](https://github.com/Puviraj-2004/hostfix/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/Puviraj-2004/hostfix?label=latest%20release)](https://github.com/Puviraj-2004/hostfix/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## Download hostfix

Download the binary for your computer. No .NET SDK or Node.js installation is required to run the standalone binaries.

### Install in one command (recommended)

**Windows Terminal / PowerShell**

```powershell
irm https://raw.githubusercontent.com/Puviraj-2004/hostfix/main/install.ps1 | iex
```

The installer downloads the latest Windows release, verifies its SHA-256 checksum, adds `hostfix` to your User PATH, and keeps the executable at `%LOCALAPPDATA%\Programs\hostfix`.

Close and reopen Windows Terminal, then confirm the installation:

```powershell
hostfix --help
```

**Linux or macOS**

```bash
curl -fsSL https://raw.githubusercontent.com/Puviraj-2004/hostfix/main/install.sh | bash
```

This installs `hostfix` to `~/.local/bin`. Open a new terminal if your shell needs to reload its PATH.

### Download manually

| Your computer | Download | Verify download |
| --- | --- | --- |
| Windows x64 | [Download `.exe`](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-win-x64.exe) | [SHA-256](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-win-x64.exe.sha256) |
| Linux x64 | [Download binary](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-linux-x64) | [SHA-256](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-linux-x64.sha256) |
| macOS Apple Silicon (M1/M2/M3/M4) | [Download binary](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-osx-arm64) | [SHA-256](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-osx-arm64.sha256) |

[View every release](https://github.com/Puviraj-2004/hostfix/releases)

## Run it

### Windows

1. Download `hostfix-win-x64.exe`.
2. Open PowerShell in the folder where it was downloaded.
3. Run:

```powershell
.\hostfix-win-x64.exe
```

To check another project, give its folder path:

```powershell
.\hostfix-win-x64.exe --path "C:\path\to\your\project"
```

### Linux

```bash
chmod +x hostfix-linux-x64
./hostfix-linux-x64
```

### macOS (Apple Silicon)

```bash
chmod +x hostfix-osx-arm64
./hostfix-osx-arm64
```

> macOS may block an app downloaded from the internet. If prompted, open **System Settings → Privacy & Security** and choose **Open Anyway** for hostfix.

## What to run

Run hostfix from the root folder of the project you want to check.

| Goal | Command after installation on PATH | Windows download command |
| --- | --- | --- |
| Check this project | `hostfix` | `.\hostfix-win-x64.exe` |
| Preview safe project repairs | `hostfix repair --dry-run` | `.\hostfix-win-x64.exe repair --dry-run` |
| Repair safe project setup issues | `hostfix repair` | `.\hostfix-win-x64.exe repair` |
| Optional machine/PATH diagnostics | `hostfix machine` | `.\hostfix-win-x64.exe machine` |
| Check Node, .NET, and Git versions | `hostfix versions` | `.\hostfix-win-x64.exe versions` |
| Check lockfiles and vulnerabilities | `hostfix deps` | `.\hostfix-win-x64.exe deps` |
| Save a Markdown report | `hostfix report --md` | `.\hostfix-win-x64.exe report --md` |
| Get JSON output for scripts | `hostfix --json` | `.\hostfix-win-x64.exe --json` |
| Set up a fresh checkout | `hostfix onboard` | `.\hostfix-win-x64.exe onboard` |

## Fix machine PATH problems safely (Windows only)

`hostfix` focuses on the current project. Machine-level PATH diagnostics are intentionally separate.

hostfix only changes your machine when you explicitly ask it to. First preview the change:

```powershell
.\hostfix-win-x64.exe machine --dry-run
```

Apply the suggested safe PATH cleanup:

```powershell
.\hostfix-win-x64.exe machine --fix
```

hostfix creates a backup before changing the Windows User PATH. Undo the most recent cleanup with:

```powershell
.\hostfix-win-x64.exe rollback
```

## Use it in CI

Create a baseline once in your project:

```bash
hostfix ci --generate-baseline
```

Then run this in CI:

```bash
hostfix ci
```

CI passes when there are no new warnings or failures compared with the saved baseline.

## Safe project repair

`hostfix repair` only changes deterministic project setup files such as `.npmrc`, `.gitignore`, and a missing `.env` template. It never upgrades a framework, invents secrets, or runs a breaking dependency update. Every repair is saved in `.hostfix/repair-sessions` and can be undone:

```powershell
hostfix rollback
```

## Checksum verification

Compare your downloaded binary with the matching SHA-256 file from the table above.

**Windows**

```powershell
Get-FileHash .\hostfix-win-x64.exe -Algorithm SHA256
```

**Linux**

```bash
sha256sum hostfix-linux-x64
```

**macOS**

```bash
shasum -a 256 hostfix-osx-arm64
```

## License

[MIT License](LICENSE)
