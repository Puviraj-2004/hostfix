using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Fixes;

/// <summary>One deterministic, project-owned text-file repair. Applied through ProjectRepairService as one journaled session.</summary>
public sealed class ProjectFileFix : IFix
{
    public string CheckId { get; }
    public string FilePath { get; }
    public string? BeforeContent { get; }
    public string AfterContent { get; }
    private readonly string _description;

    public ProjectFileFix(string checkId, string filePath, string? beforeContent, string afterContent, string description)
    {
        CheckId = checkId; FilePath = filePath; BeforeContent = beforeContent; AfterContent = afterContent; _description = description;
    }

    public bool CanApply() => !string.Equals(BeforeContent, AfterContent, StringComparison.Ordinal);
    public string Describe() => _description;
    public string DryRun() => $"--- {FilePath}{Environment.NewLine}+++ {FilePath}{Environment.NewLine}{AfterContent}";
    public Task ApplyAsync(CancellationToken cancellationToken = default) =>
        ProjectRepairService.ApplyAsync(Path.GetDirectoryName(FilePath)!, [this], cancellationToken);
    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        ProjectRepairService.RollbackLatestAsync(Path.GetDirectoryName(FilePath)!, cancellationToken);
}

public static class ProjectRepairService
{
    private sealed record Snapshot(string FilePath, bool Existed, string? Content);
    private sealed record Journal(string SessionId, DateTimeOffset CreatedAtUtc, IReadOnlyList<Snapshot> Snapshots);

    public static async Task ApplyAsync(string projectRoot, IReadOnlyList<ProjectFileFix> fixes, CancellationToken cancellationToken = default)
    {
        var applicable = fixes.Where(f => f.CanApply()).ToList();
        if (applicable.Count == 0) return;

        var sessionId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmssfff");
        var directory = Path.Combine(projectRoot, ".hostfix", "repair-sessions", sessionId);
        Directory.CreateDirectory(directory);
        var journal = new Journal(sessionId, DateTimeOffset.UtcNow, applicable.Select(f => new Snapshot(f.FilePath, f.BeforeContent is not null, f.BeforeContent)).ToArray());
        await File.WriteAllTextAsync(Path.Combine(directory, "repair.json"), System.Text.Json.JsonSerializer.Serialize(journal), cancellationToken);

        foreach (var fix in applicable)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fix.FilePath)!);
            await File.WriteAllTextAsync(fix.FilePath, fix.AfterContent, cancellationToken);
        }
    }

    public static async Task<bool> RollbackLatestAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(projectRoot, ".hostfix", "repair-sessions");
        if (!Directory.Exists(root)) return false;
        var journalPath = Directory.EnumerateFiles(root, "repair.json", SearchOption.AllDirectories).OrderByDescending(x => x).FirstOrDefault();
        if (journalPath is null) return false;
        var journal = System.Text.Json.JsonSerializer.Deserialize<Journal>(await File.ReadAllTextAsync(journalPath, cancellationToken));
        if (journal is null) return false;
        foreach (var snapshot in journal.Snapshots)
        {
            if (snapshot.Existed) await File.WriteAllTextAsync(snapshot.FilePath, snapshot.Content ?? string.Empty, cancellationToken);
            else if (File.Exists(snapshot.FilePath)) File.Delete(snapshot.FilePath);
        }
        return true;
    }
}
