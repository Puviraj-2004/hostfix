# Workspace Health Report: puvi-doctor

- **Generated At:** 2026-10-15 09:56:01 UTC
- **PuviDoctor Version:** v0.4.0
- **Health Score:** `5/10`
- **Exit Code:** `2`

## Summary

| Status | Count |
| :--- | :--- |
| Passed (OK) | 4 |
| Warnings | 3 |
| Failures | 1 |
| Skipped | 4 |

## Detailed Findings

| Check ID | Status | Message | Suggested Fix |
| :--- | :--- | :--- | :--- |
| `path.dead-entries` | **WARN** | 3 entries in PATH point to directories that do not exist. | `Sanitize Windows User PATH: remove non-existent directories and duplicate entries, preserving variable expansions.` |
| `path.duplicate-entries` | **WARN** | 29 duplicate entries found in PATH. | `Sanitize Windows User PATH: remove non-existent directories and duplicate entries, preserving variable expansions.` |
| `path.shadowing` | **WARN** | 2 tool(s) have shadowing duplicates on PATH. | - Tool 'node' has 2 instances on PATH. Active winner: 'C:\Program Files\nodejs\node.exe'. Shadowed: 'C:\Users\DVM\AppData\Local\nvm\nodejs\node.exe'   - Tool 'npm' has 4 instances on PATH. Active winner: 'C:\Program Files\nodejs\npm'. Shadowed: 'C:\Program Files\nodejs\npm.cmd', 'C:\Users\DVM\AppData\Local\nvm\nodejs\npm', 'C:\Users\DVM\AppData\Local\nvm\nodejs\npm.cmd' |
| `version.node` | **FAIL** | Node.js version mismatch: Installed v24.11.1, required '20.18.0' (from .nvmrc). | Suggested fix: Install compatible Node.js version via mise, nvm, or official installer. |
| `version.dotnet` | **PASS** | .NET SDK 9.0.306 satisfies required '9.0.100' (rollForward: latestFeature). | None |
| `version.git` | **PASS** | Git 2.49.0 installed and up to date. | None |
| `deps.lockfile` | **SKIP** | Lockfile check skipped: No package.json found. | None |
| `deps.npmrc` | **SKIP** | .npmrc check skipped: Not a Node.js project. | None |
| `deps.npm-audit` | **SKIP** | npm audit skipped: package.json or package-lock.json not found. | None |
| `deps.nuget-vulns` | **PASS** | NuGet packages: 0 known vulnerabilities found across direct and transitive dependencies. | None |
| `hygiene.env` | **SKIP** | Environment check skipped: Neither .env nor .env.example found. | None |
| `hygiene.ports` | **PASS** | Standard development ports (5173 Vite, 5000 .NET API, 8081 Expo) are free. | None |

---
*Generated automatically by [puvi-doctor](https://github.com/inspire-associates/puvi-doctor)*
