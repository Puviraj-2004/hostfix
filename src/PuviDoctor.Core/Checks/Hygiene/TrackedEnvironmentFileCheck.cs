using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Hygiene;

/// <summary>Detects local environment files committed to Git without ever reading their values.</summary>
public sealed class TrackedEnvironmentFileCheck(IFileSystem fileSystem, IProcessRunner processRunner) : IHealthCheck
{
    public string Id => "hygiene.tracked-env";

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var candidates = new[] { ".env", ".env.local", ".env.production", ".env.development" }
            .Where(name => fileSystem.FileExists(IOPath.Combine(context.ProjectRootDirectory, name))).ToArray();
        if (candidates.Length == 0) return new(CheckStatus.Skip, "Tracked environment check skipped: no local environment file found.");

        try
        {
            var result = await processRunner.ExecuteInDirectoryAsync("git", ["ls-files", "--", .. candidates], context.ProjectRootDirectory, cancellationToken);
            var tracked = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return tracked.Length > 0
                ? new(CheckStatus.Fail, $"{tracked.Length} local environment file(s) are tracked by Git.", "Secrets may already be in repository history. Hostfix will not rewrite Git history automatically.")
                : new(CheckStatus.Ok, "Local environment files are not tracked by Git.");
        }
        catch
        {
            return new(CheckStatus.Skip, "Tracked environment check skipped: Git repository status unavailable.");
        }
    }
}
