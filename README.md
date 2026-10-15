# hostfix

> Diagnose whether a developer machine is ready to build and run a repository—and explain exactly what needs attention.

[![CI & Automated Test Matrix](https://github.com/Puviraj-2004/hostfix/actions/workflows/ci.yml/badge.svg)](https://github.com/Puviraj-2004/hostfix/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

`hostfix` is a project-aware developer-environment diagnostic CLI. Run it from a repository to check the machine, project configuration, dependencies, environment files, and common development ports. It separates actionable failures from harmless differences and offers one deliberate, reversible repair: cleaning the Windows **User PATH**.

## Quick start

Install the .NET global tool:

```bash
dotnet tool install -g hostfix
```

Run it in any repository:

```bash
cd path/to/your/repository
hostfix
```

Target a repository without changing directory:

```bash
hostfix --path path/to/your/repository
```

Update or remove the tool:

```bash
dotnet tool update -g hostfix
dotnet tool uninstall -g hostfix
```

> **Prerequisite:** .NET SDK 9 or later is required to run this version of the tool.

## What hostfix checks

Checks are project-aware: a Node-specific check is skipped when no `package.json` exists, and a .NET SDK check is skipped when no `global.json` exists.

| Area | Checks | Why it matters |
| --- | --- | --- |
| PATH health | Dead entries, duplicate entries, executable shadowing | Finds stale directories and conflicting tool installations that make a different executable run than expected. |
| Runtime versions | Node.js, .NET SDK, Git | Compares Node against `.nvmrc`, `.node-version`, or `package.json`; compares .NET against `global.json`; requires Git 2.30+. |
| Dependency hygiene | Lockfiles, `.npmrc`, npm audit, NuGet vulnerabilities | Flags conflicting or missing lockfiles, missing `engine-strict`, production versus dev-only npm vulnerabilities, and vulnerable direct/transitive NuGet packages. |
| Workspace hygiene | `.env` parity and common port collisions | Confirms `.env` includes required keys from `.env.example` without printing secret values, and checks ports `5173`, `5000`, and `8081`. |

## Everyday commands

```bash
# Run every applicable diagnostic for the current repository
hostfix

# Inspect only one category
hostfix path
hostfix versions
hostfix deps

# Get automation-friendly JSON (schema version 1)
hostfix --json

# Create a shareable Markdown report in the target repository
hostfix report --md

# Prepare a fresh developer checkout: diagnose, then restore npm/.NET dependencies when applicable
hostfix onboard
```

`hostfix report --md` creates `HOSTFIX_REPORT.md` in the target repository.

## Safe PATH repair and rollback

`hostfix` does **not** silently change your machine. Auto-repair is limited to the Windows **User PATH** and removes only duplicate entries and entries pointing to directories that no longer exist. Entries containing environment variables such as `%USERPROFILE%` are preserved.

Preview the exact change first:

```bash
hostfix --dry-run
```

Apply after confirmation:

```bash
hostfix --fix
```

For non-interactive scripts, explicitly opt in:

```bash
hostfix --fix --yes
```

Before the registry is changed, hostfix saves the original Windows User PATH value and type in a local journal. Undo the most recent repair with:

```bash
hostfix rollback
```

## CI mode

CI mode reports only **new** warnings or failures relative to a repository baseline. This lets teams acknowledge existing findings while blocking new environment regressions.

Create the baseline deliberately and commit it:

```bash
hostfix ci --generate-baseline
git add .hostfix.baseline.json
git commit -m "chore: add hostfix baseline"
```

Run the comparison in CI:

```bash
hostfix ci
```

Without `.hostfix.baseline.json`, CI runs in strict mode. It exits with code `2` when it finds a warning or failure. A clean result exits with `0`.

GitHub Actions example:

```yaml
- name: Check developer-environment regressions
  run: hostfix ci
```

## Options

| Option | Meaning |
| --- | --- |
| `-p`, `--path <path>` | Repository directory to inspect. Defaults to the current directory. |
| `--json` | Emit machine-readable diagnostic output. |
| `--dry-run` | Show the proposed Windows User PATH repair without changing anything. |
| `--fix` | Prompt to apply available safe repairs. |
| `--yes` | Confirm all repairs when used with `--fix`. |
| `--help` | Show the complete command help. |

## Scope and safety notes

- Diagnostics run on Windows, Linux, and macOS; the automated test matrix validates all three.
- The only machine-configuration mutation is the opt-in Windows User PATH repair described above. It does not run `npm audit fix`, modify dependency manifests, kill processes, or read/print `.env` values.
- npm and NuGet vulnerability checks may access their package registries and can report a warning when those services are unavailable.
- `onboard` restores dependencies with `npm ci` (falling back to `npm install`) and `dotnet restore` when the target project needs them. Review its output before treating the workspace as ready.

## Develop locally

```bash
git clone https://github.com/Puviraj-2004/hostfix.git
cd hostfix
dotnet test -c Release
dotnet run --project src/PuviDoctor.Cli -- --help
```

## License

Distributed under the [MIT License](LICENSE).
