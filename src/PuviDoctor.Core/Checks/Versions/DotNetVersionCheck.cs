using System.Text.Json;
using System.Text.RegularExpressions;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using Semver;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Versions;

/// <summary>
/// Verifies that the installed .NET SDK satisfies project requirements specified in global.json.
/// Handles SemVer comparison and rollForward policy expectations.
/// </summary>
public sealed partial class DotNetVersionCheck : IHealthCheck
{
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;

    public string Id => "version.dotnet";

    public DotNetVersionCheck(IFileSystem fileSystem, IProcessRunner processRunner)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var globalJsonPath = IOPath.Combine(context.ProjectRootDirectory, "global.json");

        if (!_fileSystem.FileExists(globalJsonPath))
        {
            return new CheckResult(
                CheckStatus.Skip,
                ".NET SDK check skipped: No global.json found in project root."
            );
        }

        // 1. Parse global.json (support both modern "sdk.version" and legacy "tools.dotnet")
        string? requestedVersionRaw = null;
        string rollForwardPolicy = "latestPatch";

        try
        {
            var content = await _fileSystem.ReadAllTextAsync(globalJsonPath, cancellationToken);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("sdk", out var sdkProp))
            {
                if (sdkProp.TryGetProperty("version", out var versionProp))
                {
                    requestedVersionRaw = versionProp.GetString()?.Trim();
                }

                if (sdkProp.TryGetProperty("rollForward", out var rollForwardProp))
                {
                    rollForwardPolicy = rollForwardProp.GetString()?.Trim() ?? "latestPatch";
                }
            }

            // Fallback to legacy tools.dotnet if not in sdk
            if (string.IsNullOrWhiteSpace(requestedVersionRaw) &&
                doc.RootElement.TryGetProperty("tools", out var tools) &&
                tools.TryGetProperty("dotnet", out var dotnetProp))
            {
                requestedVersionRaw = dotnetProp.GetString()?.Trim();
            }

            if (string.IsNullOrWhiteSpace(requestedVersionRaw))
            {
                return new CheckResult(
                    CheckStatus.Skip,
                    ".NET SDK check skipped: Neither 'sdk.version' nor 'tools.dotnet' specified in global.json."
                );
            }
        }
        catch (JsonException ex)
        {
            return new CheckResult(
                CheckStatus.Fail,
                $"Invalid global.json syntax: {ex.Message}"
            );
        }

        if (!SemVersion.TryParse(requestedVersionRaw, SemVersionStyles.Strict, out var requestedSemver))
        {
            return new CheckResult(
                CheckStatus.Fail,
                $"Cannot parse required .NET SDK version '{requestedVersionRaw}' in global.json as valid SemVer."
            );
        }

        // 2. Query installed .NET SDKs
        ProcessOutput output;
        try
        {
            output = await _processRunner.ExecuteAsync("dotnet", ["--list-sdks"], cancellationToken);
        }
        catch (Exception ex)
        {
            return new CheckResult(
                CheckStatus.Fail,
                $"Failed to execute 'dotnet --list-sdks': {ex.Message}. Is .NET installed?"
            );
        }

        if (output.ExitCode != 0 || string.IsNullOrWhiteSpace(output.StandardOutput))
        {
            return new CheckResult(
                CheckStatus.Fail,
                "No .NET SDKs found on system PATH."
            );
        }

        var installedSdks = ParseInstalledSdks(output.StandardOutput);

        if (installedSdks.Count == 0)
        {
            return new CheckResult(
                CheckStatus.Fail,
                "Unable to identify any installed .NET SDK versions from 'dotnet --list-sdks'."
            );
        }

        // 3. Match against rollForward rules
        var matchingSdk = FindSatisfyingSdk(installedSdks, requestedSemver, rollForwardPolicy);

        if (matchingSdk is not null)
        {
            return new CheckResult(
                CheckStatus.Ok,
                $".NET SDK {matchingSdk} satisfies required '{requestedVersionRaw}' (rollForward: {rollForwardPolicy})."
            );
        }

        var availableList = string.Join(", ", installedSdks.Select(s => s.ToString()));
        return new CheckResult(
            CheckStatus.Fail,
            $".NET SDK mismatch: Required '{requestedVersionRaw}' (rollForward: {rollForwardPolicy}) not satisfied.",
            $"Installed SDKs: [{availableList}]. Fix: Run 'winget install Microsoft.DotNet.SDK.{requestedSemver.Major}'"
        );
    }

    private static List<SemVersion> ParseInstalledSdks(string rawOutput)
    {
        var result = new List<SemVersion>();
        var lines = rawOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var match = SdkLineRegex().Match(line);
            if (match.Success && SemVersion.TryParse(match.Groups[1].Value, SemVersionStyles.Strict, out var semver))
            {
                result.Add(semver);
            }
        }

        // Production fix: Semver 3.0 requires explicit SortOrderComparer
        return result.OrderByDescending(x => x, SemVersion.SortOrderComparer).ToList();
    }

    private static SemVersion? FindSatisfyingSdk(IEnumerable<SemVersion> installed, SemVersion target, string rollForward)
    {
        return rollForward.ToLowerInvariant() switch
        {
            "disable" => installed.FirstOrDefault(s => s.Equals(target)),
            "latestpatch" => installed.FirstOrDefault(s => s.Major == target.Major && s.Minor == target.Minor && s.Patch >= target.Patch),
            "latestfeature" => installed.FirstOrDefault(s => s.Major == target.Major && s.Minor >= target.Minor),
            "latestmajor" => installed.FirstOrDefault(s => s.Major >= target.Major),
            _ => installed.FirstOrDefault(s => s.Major == target.Major && s.Minor == target.Minor && s.Patch >= target.Patch)
        };
    }

    [GeneratedRegex(@"^([0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9\.]+)?)\s+\[.+\]$")]
    private static partial Regex SdkLineRegex();
}