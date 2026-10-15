using System.Text.RegularExpressions;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using Semver;

namespace PuviDoctor.Core.Checks.Versions;

/// <summary>
/// Ensures Git is installed on the host and satisfies the minimum recommended version (2.30.0).
/// </summary>
public sealed partial class GitVersionCheck : IHealthCheck
{
    private readonly IProcessRunner _processRunner;
    private static readonly SemVersion MinimumGitVersion = new(2, 30, 0);

    public string Id => "version.git";

    public GitVersionCheck(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        ProcessOutput output;
        try
        {
            output = await _processRunner.ExecuteAsync("git", ["--version"], cancellationToken);
        }
        catch (Exception ex)
        {
            return new CheckResult(
                CheckStatus.Fail,
                $"Failed to execute 'git --version': {ex.Message}. Git is not installed."
            );
        }

        if (output.ExitCode != 0 || string.IsNullOrWhiteSpace(output.StandardOutput))
        {
            return new CheckResult(
                CheckStatus.Fail,
                "Git is not found on system PATH."
            );
        }

        // git version 2.45.2.windows.1 or git version 2.39.2
        var match = GitVersionRegex().Match(output.StandardOutput);
        if (!match.Success || !SemVersion.TryParse(match.Groups[1].Value, SemVersionStyles.Strict, out var installedSemver))
        {
            return new CheckResult(
                CheckStatus.Warn,
                $"Git found, but unable to parse version from output: '{output.StandardOutput}'."
            );
        }

        if (SemVersion.CompareSortOrder(installedSemver, MinimumGitVersion) < 0)
        {
            return new CheckResult(
                CheckStatus.Fail,
                $"Git version {installedSemver} is below minimum requirement ({MinimumGitVersion}).",
                "Fix: Update Git via 'winget upgrade Git.Git' or your platform package manager."
            );
        }

        return new CheckResult(
            CheckStatus.Ok,
            $"Git {installedSemver} installed and up to date."
        );
    }

    [GeneratedRegex(@"git\s+version\s+([0-9]+\.[0-9]+\.[0-9]+)")]
    private static partial Regex GitVersionRegex();
}