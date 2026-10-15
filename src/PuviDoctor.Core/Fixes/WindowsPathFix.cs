using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using PuviDoctor.Core.Platform;

namespace PuviDoctor.Core.Fixes;

/// <summary>
/// Safe, reversible fix for Windows User PATH anomalies (duplicates and dead entries).
/// Preserves REG_EXPAND_SZ, persists before-state session journal, and supports rollback.
/// </summary>
public sealed class WindowsPathFix : IFix
{
    private readonly IRegistryService _registryService;
    private readonly IEnvironmentService _environmentService;
    private readonly string _checkId;

    public WindowsPathFix(
        IRegistryService registryService,
        IEnvironmentService environmentService,
        string checkId)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        _checkId = checkId ?? throw new ArgumentNullException(nameof(checkId));
    }

    public string Describe()
    {
        return "Sanitize Windows User PATH: remove non-existent directories and duplicate entries, preserving variable expansions.";
    }

    public string DryRun()
    {
        var snapshot = _registryService.GetUserPathSnapshot();
        if (string.IsNullOrWhiteSpace(snapshot.RawValue))
        {
            return "No changes needed: User PATH is empty.";
        }

        var cleanedPath = ComputeCleanedPath(snapshot.RawValue);

        return $"""
        === Dry Run PATH Diff ===
        Original ({snapshot.Kind}):
          {snapshot.RawValue}

        Proposed ({snapshot.Kind}):
          {cleanedPath}
        =========================
        """;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _registryService.GetUserPathSnapshot();
        if (string.IsNullOrWhiteSpace(snapshot.RawValue))
        {
            return;
        }

        // 1. Mandatory safety: Journal before-state to disk BEFORE mutating registry
        var sessionId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmssfff");
        var journal = new FixSessionJournal(
            SessionId: sessionId,
            TimestampUtc: DateTimeOffset.UtcNow,
            CheckId: _checkId,
            Description: Describe(),
            BeforeUserPathSnapshot: snapshot
        );

        await FixJournalStore.SaveSessionAsync(journal, cancellationToken);

        // 2. Compute sanitized PATH
        var cleanedPath = ComputeCleanedPath(snapshot.RawValue);

        // 3. Write directly to HKCU\Environment (never setx)
        _registryService.SetUserPath(cleanedPath, snapshot.Kind);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        var latestSession = await FixJournalStore.GetLatestSessionAsync(cancellationToken);
        if (latestSession is null || latestSession.BeforeUserPathSnapshot.RawValue is null)
        {
            throw new InvalidOperationException("No recorded session found for rollback.");
        }

        // Restore exact byte-level prior string and registry value kind
        _registryService.SetUserPath(
            latestSession.BeforeUserPathSnapshot.RawValue,
            latestSession.BeforeUserPathSnapshot.Kind
        );
    }

    private string ComputeCleanedPath(string rawPath)
    {
        var entries = rawPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keptEntries = new List<string>();

        foreach (var entry in entries)
        {
            var trimmed = entry.Trim().Trim('"');
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            // Remove trailing slashes for duplicate deduplication
            var normalizedKey = trimmed.TrimEnd('\\', '/').ToUpperInvariant();

            // 1. Filter out duplicates
            if (!seen.Add(normalizedKey))
            {
                continue;
            }

            // 2. Filter out non-existent folders (unless it's an unexpanded variable like %USERPROFILE%)
            if (!trimmed.StartsWith('%') && !_environmentService.DirectoryExists(trimmed))
            {
                continue;
            }

            keptEntries.Add(trimmed);
        }

        return string.Join(";", keptEntries);
    }
}