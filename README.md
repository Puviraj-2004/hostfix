# hostfix

> A fast, project-aware developer machine diagnostics, PATH sanitizer, and environment repair CLI.  
> Answers one question in under 1 second: **Can this machine build and run this repo, and if not, what exactly is wrong?**

[![CI & Health Check](https://github.com/Puviraj-2004/hostfix/actions/workflows/ci.yml/badge.svg)](https://github.com/Puviraj-2004/hostfix/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## The Pains It Solves

- **Machine Pain:** Duplicate or dead PATH entries, tools shadowing each other (e.g. global Node installs shadowing mise/nvm shims), and dirty environment setups.
- **Project Pain:** Tool-version mismatches, surprise vulnerabilities from uncurated `npm audit`, port collisions (`5173`, `5000`, `8081`), and missing `.env` variables.
- **Day-1 Onboarding:** Onboard a new developer to a repository with one command instead of hours of manual debugging.

---

## Installation

### Option 1: .NET Global Tool
```bash
dotnet tool install -g hostfix