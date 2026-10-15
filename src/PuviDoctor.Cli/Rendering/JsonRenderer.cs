using System.Text.Json;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Cli.Rendering;

/// <summary>
/// Generates stable JSON output schema (schema: 1) for CI, pipelines, and machine consumption.
/// </summary>
public static class JsonRenderer
{
    private sealed record JsonReportSchema(
        int SchemaVersion,
        string ProjectName,
        DateTimeOffset GeneratedAtUtc,
        int Score,
        int MaxScore,
        int ExitCode,
        IReadOnlyList<JsonCheckResult> Findings
    );

    private sealed record JsonCheckResult(
        string CheckId,
        string Status,
        string Message,
        string? Details,
        bool IsAutoFixable
    );

    public static string Render(HealthReport report, string projectName)
    {
        var findings = report.Executions.Select(e => new JsonCheckResult(
            CheckId: e.Check.Id,
            Status: e.Result.Status.ToString().ToUpperInvariant(),
            Message: e.Result.Message,
            Details: e.Result.Details,
            IsAutoFixable: e.Result.Fix is not null
        )).ToList();

        var schema = new JsonReportSchema(
            SchemaVersion: 1,
            ProjectName: projectName,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Score: report.Score,
            MaxScore: report.MaxScore,
            ExitCode: report.ExitCode,
            Findings: findings
        );

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}