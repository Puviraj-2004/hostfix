using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Dependencies;

/// <summary>
/// Audits .NET projects for known vulnerable dependencies using 'dotnet list package --vulnerable --include-transitive'.
/// </summary>
public sealed class NuGetVulnsCheck : IHealthCheck
{
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;

    public string Id => "deps.nuget-vulns";

    public NuGetVulnsCheck(IFileSystem fileSystem, IProcessRunner processRunner)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        // Check if repo has a solution or csproj file
        var hasSln = _fileSystem.FileExists(IOPath.Combine(context.ProjectRootDirectory, "global.json")) ||
                     _fileSystem.GetFiles(context.ProjectRootDirectory, "*.slnx").Any() ||
                     _fileSystem.GetFiles(context.ProjectRootDirectory, "*.sln").Any() ||
                     _fileSystem.GetFiles(context.ProjectRootDirectory, "*.csproj").Any();

        if (!hasSln)
        {
            return new CheckResult(
                CheckStatus.Skip,
                "NuGet vulnerability check skipped: No .NET solution found in project root."
            );
        }

        if (context.IsOfflineMode)
        {
            return new CheckResult(
                CheckStatus.Skip,
                "NuGet vulnerability check skipped in offline mode."
            );
        }

        ProcessOutput output;
        try
        {
            output = await _processRunner.ExecuteInDirectoryAsync(
                "dotnet",
                ["list", "package", "--vulnerable", "--include-transitive"],
                context.ProjectRootDirectory,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return new CheckResult(
                CheckStatus.Warn,
                $"Failed to query NuGet vulnerabilities: {ex.Message}"
            );
        }

        if (output.StandardOutput.Contains("has no vulnerable packages", StringComparison.OrdinalIgnoreCase))
        {
            return new CheckResult(
                CheckStatus.Ok,
                "NuGet packages: 0 known vulnerabilities found across direct and transitive dependencies."
            );
        }

        if (output.ExitCode != 0 || output.StandardOutput.Contains("Critical", StringComparison.OrdinalIgnoreCase) ||
            output.StandardOutput.Contains("High", StringComparison.OrdinalIgnoreCase))
        {
            return new CheckResult(
                CheckStatus.Fail,
                "Vulnerable NuGet packages detected in .NET dependency graph.",
                output.StandardOutput
            );
        }

        return new CheckResult(
            CheckStatus.Ok,
            "NuGet package vulnerability check completed cleanly."
        );
    }
}
