using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Reporting;

public sealed record DeepRepairResult(bool Started, string Message, string? BranchName, VerificationReport? Verification);

/// <summary>Contains dependency repair attempts in a new Git branch. It never runs a breaking --force update.</summary>
public sealed class DeepRepairService(IFileSystem fileSystem, IProcessRunner processRunner)
{
    public async Task<DeepRepairResult> RunAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        var git = await processRunner.ExecuteInDirectoryAsync("git", ["status", "--porcelain"], projectRoot, cancellationToken);
        if (git.ExitCode != 0) return new(false, "Deep repair requires a Git repository.", null, null);
        if (!string.IsNullOrWhiteSpace(git.StandardOutput)) return new(false, "Deep repair requires a clean working tree. Commit or stash current changes first.", null, null);

        var branch = $"hostfix/repair-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        var create = await processRunner.ExecuteInDirectoryAsync("git", ["switch", "-c", branch], projectRoot, cancellationToken);
        if (create.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(create.StandardError) ? "Unable to create repair branch." : create.StandardError.Trim(), null, null);

        if (fileSystem.FileExists(Path.Combine(projectRoot, "package.json")) && fileSystem.FileExists(Path.Combine(projectRoot, "package-lock.json")))
        {
            // npm audit fix deliberately excludes --force: major framework migrations stay review-only.
            await processRunner.ExecuteInDirectoryAsync("npm", ["audit", "fix"], projectRoot, cancellationToken);
        }

        var verification = await new ProjectVerifier(fileSystem, processRunner).VerifyAsync(projectRoot, cancellationToken);
        return new(true, verification.Passed ? "Deep repair branch is verified and ready for review." : "Deep repair branch was created, but verification still fails. Your original branch remains untouched.", branch, verification);
    }
}
