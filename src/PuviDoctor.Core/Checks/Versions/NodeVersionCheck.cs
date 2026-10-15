using System.Text.Json;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using Semver;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Versions;

/// <summary>
/// Validates installed Node.js runtime against project-defined version requirements (.nvmrc, .node-version, package.json).
/// </summary>
public sealed class NodeVersionCheck : IHealthCheck
{
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;

    public string Id => "version.node";

    public NodeVersionCheck(IFileSystem fileSystem, IProcessRunner processRunner)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        // 1. Detect required version from project files
        var (requiredVersionRaw, sourceFile) = await ResolveRequiredVersionAsync(context.ProjectRootDirectory, cancellationToken);

        if (string.IsNullOrWhiteSpace(requiredVersionRaw))
        {
            return new CheckResult(
                CheckStatus.Skip,
                "Node.js check skipped: No .nvmrc, .node-version, or package.json engines.node found."
            );
        }

        // 2. Query installed Node.js version
        ProcessOutput output;
        try
        {
            output = await _processRunner.ExecuteAsync("node", ["--version"], cancellationToken);
        }
        catch (Exception ex)
        {
            return new CheckResult(
                CheckStatus.Fail,
                $"Failed to invoke Node.js: {ex.Message}. Is Node.js installed?"
            );
        }

        if (output.ExitCode != 0 || string.IsNullOrWhiteSpace(output.StandardOutput))
        {
            return new CheckResult(
                CheckStatus.Fail,
                "Node.js is not installed or not available on PATH."
            );
        }

        var installedRaw = output.StandardOutput.Trim().TrimStart('v');
        if (!SemVersion.TryParse(installedRaw, SemVersionStyles.Strict, out var installedSemver))
        {
            return new CheckResult(
                CheckStatus.Fail,
                $"Could not parse installed Node.js version '{output.StandardOutput.Trim()}' as valid SemVer."
            );
        }

        // 3. Normalize requirement and validate
        var cleanRequired = requiredVersionRaw.Trim().TrimStart('v');

        // Check if it's a simple wild-card (e.g., "20.x" or "20.*")
        if (cleanRequired.EndsWith(".x", StringComparison.OrdinalIgnoreCase) ||
            cleanRequired.EndsWith(".*", StringComparison.OrdinalIgnoreCase))
        {
            var majorPart = cleanRequired[..^2];
            if (int.TryParse(majorPart, out var requiredMajor) && installedSemver.Major == requiredMajor)
            {
                return new CheckResult(
                    CheckStatus.Ok,
                    $"Node.js v{installedSemver} satisfies '{requiredVersionRaw}' (from {sourceFile})."
                );
            }
        }
        else if (SemVersionRange.TryParseNpm(cleanRequired, out var range))
        {
            if (range.Contains(installedSemver))
            {
                return new CheckResult(
                    CheckStatus.Ok,
                    $"Node.js v{installedSemver} satisfies '{requiredVersionRaw}' (from {sourceFile})."
                );
            }
        }
        else if (SemVersion.TryParse(cleanRequired, SemVersionStyles.Any, out var exactSemver))
        {
            if (installedSemver.Major == exactSemver.Major && installedSemver.Minor == exactSemver.Minor)
            {
                return new CheckResult(
                    CheckStatus.Ok,
                    $"Node.js v{installedSemver} matches requested '{requiredVersionRaw}' (from {sourceFile})."
                );
            }
        }

        return new CheckResult(
            CheckStatus.Fail,
            $"Node.js version mismatch: Installed v{installedSemver}, required '{requiredVersionRaw}' (from {sourceFile}).",
            "Suggested fix: Install compatible Node.js version via mise, nvm, or official installer."
        );
    }

    private async Task<(string? Version, string? Source)> ResolveRequiredVersionAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        // Priority 1: .nvmrc
        var nvmrcPath = IOPath.Combine(projectRoot, ".nvmrc");
        if (_fileSystem.FileExists(nvmrcPath))
        {
            var content = (await _fileSystem.ReadAllTextAsync(nvmrcPath, cancellationToken)).Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                return (content, ".nvmrc");
            }
        }

        // Priority 2: .node-version
        var nodeVersionPath = IOPath.Combine(projectRoot, ".node-version");
        if (_fileSystem.FileExists(nodeVersionPath))
        {
            var content = (await _fileSystem.ReadAllTextAsync(nodeVersionPath, cancellationToken)).Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                return (content, ".node-version");
            }
        }

        // Priority 3: package.json -> engines.node
        var packageJsonPath = IOPath.Combine(projectRoot, "package.json");
        if (_fileSystem.FileExists(packageJsonPath))
        {
            try
            {
                var json = await _fileSystem.ReadAllTextAsync(packageJsonPath, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("engines", out var engines) &&
                    engines.TryGetProperty("node", out var nodeEngine))
                {
                    var versionStr = nodeEngine.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(versionStr))
                    {
                        return (versionStr, "package.json");
                    }
                }
            }
            catch (JsonException)
            {
                // Broken package.json will be handled by package.json hygiene check
            }
        }

        return (null, null);
    }
}