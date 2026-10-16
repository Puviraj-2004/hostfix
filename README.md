# hostfix

[![CI](https://github.com/Puviraj-2004/hostfix/actions/workflows/ci.yml/badge.svg)](https://github.com/Puviraj-2004/hostfix/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/Puviraj-2004/hostfix?label=latest%20release)](https://github.com/Puviraj-2004/hostfix/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## Install once, use in every project

`hostfix` is a standalone CLI. You do **not** need to install .NET or Node.js to run it. Install it once; afterwards, open a terminal in any project and run `hostfix`.

### Windows x64 — PowerShell

```powershell
irm https://raw.githubusercontent.com/Puviraj-2004/hostfix/main/install.ps1 | iex
```

The installer downloads the latest Windows release, verifies its SHA-256 checksum, and adds `hostfix` to your **User PATH**. The executable is stored at `%LOCALAPPDATA%\Programs\hostfix\hostfix.exe`.

Close and reopen Windows Terminal, then confirm the installation:

```powershell
hostfix --help
```

### Linux x64 — Bash

```bash
curl -fsSL https://raw.githubusercontent.com/Puviraj-2004/hostfix/main/install.sh | bash
```

The installer installs `hostfix` to `~/.local/bin`, verifies its SHA-256 checksum, and adds that folder to your shell PATH when needed. Close and reopen the terminal, then run:

```bash
hostfix --help
```

### macOS Apple Silicon (M1/M2/M3/M4) — Terminal

```bash
curl -fsSL https://raw.githubusercontent.com/Puviraj-2004/hostfix/main/install.sh | bash
```

The installer installs `hostfix` to `~/.local/bin`, verifies its SHA-256 checksum, and updates your shell PATH when needed. Close and reopen Terminal, then run:

```bash
hostfix --help
```

> macOS Intel is not supported yet. macOS may ask for confirmation for an internet-downloaded app: open **System Settings → Privacy & Security** and select **Open Anyway** for hostfix.

### Download manually

| Your computer | Download | Verify download |
| --- | --- | --- |
| Windows x64 | [Download `.exe`](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-win-x64.exe) | [SHA-256](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-win-x64.exe.sha256) |
| Linux x64 | [Download binary](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-linux-x64) | [SHA-256](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-linux-x64.sha256) |
| macOS Apple Silicon (M1/M2/M3/M4) | [Download binary](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-osx-arm64) | [SHA-256](https://github.com/Puviraj-2004/hostfix/releases/latest/download/hostfix-osx-arm64.sha256) |

[View every release](https://github.com/Puviraj-2004/hostfix/releases)

## Use it in any project

Open Terminal, PowerShell, or your IDE terminal in the project's root folder:

```bash
cd path/to/your-project
hostfix
```

To check a project from somewhere else:

```powershell
hostfix --path "C:\path\to\your\project"
```

```bash
hostfix --path "/path/to/your/project"
```

## Command reference

| Goal | Command | What it does |
| --- | --- | --- |
| Check this project | `hostfix` | Shows project-specific health issues and priorities. |
| Verify it really works | `hostfix verify` | Restores dependencies, then runs detected build, lint, typecheck, and test commands with live progress. |
| Check runtime versions | `hostfix versions` | Checks required Node.js, .NET, and Git versions. |
| Check dependencies | `hostfix deps` | Checks lockfiles and reports npm/NuGet vulnerabilities. |
| Preview safe fixes | `hostfix repair --dry-run` | Shows project file changes without modifying anything. |
| Apply safe fixes | `hostfix repair` | Repairs deterministic project setup files and re-checks the project. |
| Try safe dependency repair | `hostfix repair --deep` | Creates an isolated Git branch, tries non-breaking dependency updates, then verifies it. |
| Undo latest safe repair | `hostfix rollback` | Restores the last Hostfix repair session. |
| Create a Markdown report | `hostfix report --md` | Writes `HOSTFIX_REPORT.md` in the target project. |
| Script-friendly result | `hostfix --json` | Outputs the default health report as JSON. |
| Set up a new checkout | `hostfix onboard` | Runs diagnostics, applies safe fixes, and restores dependencies. |
| CI regression check | `hostfix ci` | Fails CI only for issues newer than `.hostfix.baseline.json`. |
| Create CI baseline | `hostfix ci --generate-baseline` | Records current accepted issues as the initial baseline. |
| Machine PATH diagnostics | `hostfix machine` | Optional Windows User PATH analysis; not part of normal project checking. |
| Preview PATH cleanup | `hostfix machine --dry-run` | Shows safe User PATH changes without applying them. |
| Apply PATH cleanup | `hostfix machine --fix` | Applies safe Windows User PATH cleanup after confirmation. |
| Apply PATH cleanup without prompts | `hostfix machine --fix --yes` | Same as above, for scripted use. |

Use help any time:

```bash
hostfix --help
hostfix repair --help
```

## Fix machine PATH problems safely (Windows only)

`hostfix` focuses on the current project. Machine-level PATH diagnostics are intentionally separate.

hostfix only changes your machine when you explicitly ask it to. First preview the change:

```powershell
hostfix machine --dry-run
```

Apply the suggested safe PATH cleanup:

```powershell
hostfix machine --fix
```

hostfix creates a backup before changing the Windows User PATH. Undo the most recent cleanup with:

```powershell
hostfix rollback
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
