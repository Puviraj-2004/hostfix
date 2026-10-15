using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Dependencies;

/// <summary>
/// Ensures repository has a consistent lockfile and flags conflicting lockfiles across package managers.
/// </summary>
public sealed class LockfileCheck : IHealthCheck
{
    private readonly IFileSystem _fileSystem;

    public string Id => "deps.lockfile";

    public LockfileCheck(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var packageJsonPath = IOPath.Combine(context.ProjectRootDirectory, "package.json");

        // If no package.json exists, this is not a Node project; skip
        if (!_fileSystem.FileExists(packageJsonPath))
        {
            return Task.FromResult(new CheckResult(
                CheckStatus.Skip,
                "Lockfile check skipped: No package.json found."
            ));
        }

        var npmLock = IOPath.Combine(context.ProjectRootDirectory, "package-lock.json");
        var yarnLock = IOPath.Combine(context.ProjectRootDirectory, "yarn.lock");
        var pnpmLock = IOPath.Combine(context.ProjectRootDirectory, "pnpm-lock.yaml");

        var hasNpm = _fileSystem.FileExists(npmLock);
        var hasYarn = _fileSystem.FileExists(yarnLock);
        var hasPnpm = _fileSystem.FileExists(pnpmLock);

        var presentCount = (hasNpm ? 1 : 0) + (hasYarn ? 1 : 0) + (hasPnpm ? 1 : 0);

        // Case 1: Multiple lockfiles conflict
        if (presentCount > 1)
        {
            var conflicts = new List<string>();
            if (hasNpm) conflicts.Add("package-lock.json (npm)");
            if (hasYarn) conflicts.Add("yarn.lock (yarn)");
            if (hasPnpm) conflicts.Add("pnpm-lock.yaml (pnpm)");

            return Task.FromResult(new CheckResult(
                CheckStatus.Fail,
                $"Conflicting lockfiles detected: [{string.Join(", ", conflicts)}].",
                "Fix: Keep only one package manager lockfile to prevent unpredictable dependency trees across environments."
            ));
        }

        // Case 2: No lockfile at all
        if (presentCount == 0)
        {
            return Task.FromResult(new CheckResult(
                CheckStatus.Warn,
                "No lockfile found (package-lock.json missing). Installs are not reproducible.",
                "Fix: Run 'npm install' or 'pnpm install' and commit the generated lockfile."
            ));
        }

        // Case 3: Clean single lockfile
        var activeManager = hasNpm ? "npm (package-lock.json)" : hasYarn ? "yarn (yarn.lock)" : "pnpm (pnpm-lock.yaml)";
        return Task.FromResult(new CheckResult(
            CheckStatus.Ok,
            $"Consistent lockfile present for {activeManager}. Installs are reproducible via CI."
        ));
    }
}