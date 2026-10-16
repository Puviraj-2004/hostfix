using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Fixes;
using PuviDoctor.Core.Models;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Dependencies;

/// <summary>
/// Audits .npmrc configuration to ensure reproducible and safe installs (e.g. engine-strict=true).
/// </summary>
public sealed class NpmrcCheck : IHealthCheck
{
    private readonly IFileSystem _fileSystem;

    public string Id => "deps.npmrc";

    public NpmrcCheck(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var packageJsonPath = IOPath.Combine(context.ProjectRootDirectory, "package.json");
        if (!_fileSystem.FileExists(packageJsonPath))
        {
            return new CheckResult(CheckStatus.Skip, ".npmrc check skipped: Not a Node.js project.");
        }

        var npmrcPath = IOPath.Combine(context.ProjectRootDirectory, ".npmrc");
        if (!_fileSystem.FileExists(npmrcPath))
        {
            const string content = "engine-strict=true\n";
            return new CheckResult(
                CheckStatus.Warn,
                "No .npmrc file found in project root.",
                "Hostfix can add engine-strict=true safely.",
                new ProjectFileFix(Id, npmrcPath, null, content, "Create .npmrc with engine-strict=true."));
        }

        var lines = (await _fileSystem.ReadAllTextAsync(npmrcPath, cancellationToken))
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool engineStrict = false;

        foreach (var line in lines)
        {
            if (line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var val = parts[1].Trim().ToLowerInvariant();

                if (key.Equals("engine-strict", StringComparison.OrdinalIgnoreCase) && val == "true")
                {
                    engineStrict = true;
                }
            }
        }

        if (!engineStrict)
        {
            return new CheckResult(
                CheckStatus.Warn,
                ".npmrc does not have 'engine-strict=true' configured.",
                "Existing .npmrc files can contain private registry credentials, so Hostfix will not rewrite this file automatically."
            );
        }

        return new CheckResult(
            CheckStatus.Ok,
            ".npmrc configuration is healthy (engine-strict=true enabled)."
        );
    }
}
