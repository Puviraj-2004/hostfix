using System.Text.Json;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using IOPath = System.IO.Path;

namespace PuviDoctor.Core.Platform;

/// <summary>
/// Manages loading and saving the repository CI baseline file (.hostfix.baseline.json).
/// Allows teams to accept existing historical debt without blocking CI on new regressions.
/// </summary>
public static class BaselineStore
{
    public const string BaselineFileName = ".hostfix.baseline.json";

    public static string GetBaselinePath(string projectRoot) => IOPath.Combine(projectRoot, BaselineFileName);

    public static async Task SaveBaselineAsync(
        string projectRoot,
        HealthReport report,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        var baselinePath = GetBaselinePath(projectRoot);

        // Include any Warn or Fail issues into the accepted baseline
        var findings = report.Executions
            .Where(e => e.Result.Status is CheckStatus.Warn or CheckStatus.Fail)
            .Select(e => new BaselineEntry(
                CheckId: e.Check.Id,
                Status: e.Result.Status.ToString().ToUpperInvariant(),
                Message: e.Result.Message
            ))
            .ToList();

        var baseline = new PuviDoctorBaseline(
            SchemaVersion: 1,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            AcceptedFindings: findings
        );

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(baseline, options);
        await File.WriteAllTextAsync(baselinePath, json, cancellationToken);
    }

    public static async Task<PuviDoctorBaseline?> LoadBaselineAsync(
        string projectRoot,
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        var baselinePath = GetBaselinePath(projectRoot);
        if (!fileSystem.FileExists(baselinePath))
        {
            return null;
        }

        var json = await fileSystem.ReadAllTextAsync(baselinePath, cancellationToken);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<PuviDoctorBaseline>(json, options);
    }
}