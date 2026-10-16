using System.Text.Json;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Checks.Dependencies;

/// <summary>
/// Triages npm audit results by splitting production vulnerabilities from dev-only vulnerabilities.
/// Prevents unnecessary developer panic and warns against blind 'npm audit fix --force'.
/// </summary>
public sealed class NpmAuditCheck : IHealthCheck
{
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;

    public string Id => "deps.npm-audit";

    public NpmAuditCheck(IFileSystem fileSystem, IProcessRunner processRunner)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var packageJsonPath = IOPath.Combine(context.ProjectRootDirectory, "package.json");
        var lockfilePath = IOPath.Combine(context.ProjectRootDirectory, "package-lock.json");

        if (!_fileSystem.FileExists(packageJsonPath) || !_fileSystem.FileExists(lockfilePath))
        {
            return new CheckResult(
                CheckStatus.Skip,
                "npm audit skipped: package.json or package-lock.json not found."
            );
        }

        if (context.IsOfflineMode)
        {
            return new CheckResult(
                CheckStatus.Skip,
                "npm audit skipped in offline mode."
            );
        }

        // 1. Run full npm audit --json
        var fullOutput = await _processRunner.ExecuteAsync("npm", ["audit", "--json"], cancellationToken);
        if (string.IsNullOrWhiteSpace(fullOutput.StandardOutput))
        {
            var reason = string.IsNullOrWhiteSpace(fullOutput.StandardError)
                ? "npm returned no JSON output. Check network access and the npm registry."
                : $"npm failed: {fullOutput.StandardError.Trim()}";
            return new CheckResult(
                CheckStatus.Warn,
                "npm audit could not produce a vulnerability report.",
                reason
            );
        }

        var fullSummary = ParseAuditSummary(fullOutput.StandardOutput);
        if (fullSummary.Total == 0)
        {
            return new CheckResult(
                CheckStatus.Ok,
                "npm audit: 0 vulnerabilities found."
            );
        }

        // 2. Run npm audit --json --omit=dev to find true production vulnerabilities
        var prodOutput = await _processRunner.ExecuteAsync("npm", ["audit", "--json", "--omit=dev"], cancellationToken);
        var prodSummary = ParseAuditSummary(prodOutput.StandardOutput);

        var devCount = Math.Max(0, fullSummary.Total - prodSummary.Total);
        var prodCount = prodSummary.Total;

        var message = $"npm audit: {fullSummary.Total} vulnerabilities ({prodCount} production, {devCount} dev-only).";

        var details =
            $"Production: {prodSummary.Critical} critical, {prodSummary.High} high, {prodSummary.Moderate} moderate, {prodSummary.Low} low." +
            Environment.NewLine +
            $"Dev-only:   {fullSummary.Critical - prodSummary.Critical} critical, {fullSummary.High - prodSummary.High} high, {fullSummary.Moderate - prodSummary.Moderate} moderate, {fullSummary.Low - prodSummary.Low} low." +
            Environment.NewLine +
            "Note: Never run 'npm audit fix --force' blindly as it introduces breaking API changes.";

        // Production vulnerabilities with high or critical severity result in FAIL
        if (prodSummary.Critical > 0 || prodSummary.High > 0)
        {
            return new CheckResult(
                CheckStatus.Fail,
                message,
                details
            );
        }

        // Dev-only or low/moderate production vulnerabilities result in WARN
        return new CheckResult(
            CheckStatus.Warn,
            message,
            details
        );
    }

    private sealed record AuditSummary(int Total, int Low, int Moderate, int High, int Critical);

    private static AuditSummary ParseAuditSummary(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("metadata", out var metadata) &&
                metadata.TryGetProperty("vulnerabilities", out var vulns))
            {
                var low = vulns.TryGetProperty("low", out var l) ? l.GetInt32() : 0;
                var mod = vulns.TryGetProperty("moderate", out var m) ? m.GetInt32() : 0;
                var high = vulns.TryGetProperty("high", out var h) ? h.GetInt32() : 0;
                var crit = vulns.TryGetProperty("critical", out var c) ? c.GetInt32() : 0;
                var total = vulns.TryGetProperty("total", out var t) ? t.GetInt32() : low + mod + high + crit;

                return new AuditSummary(total, low, mod, high, crit);
            }
        }
        catch (JsonException)
        {
            // Invalid JSON
        }

        return new AuditSummary(0, 0, 0, 0, 0);
    }
}
