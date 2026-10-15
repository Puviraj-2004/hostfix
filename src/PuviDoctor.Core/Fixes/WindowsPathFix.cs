using System.Text.RegularExpressions;
using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;
using PuviDoctor.Core.Platform;

namespace PuviDoctor.Core.Fixes;

/// <summary>
/// Safe, reversible fix for Windows User PATH anomalies.
/// Preserves REG_EXPAND_SZ, prevents multiple overwrite regressions, and ensures byte-identical rollback.
/// </summary>
public sealed partial class WindowsPathFix : IFix
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

        var cleanedPath = ComputeCleanedPath(snapshot.RawValue);

        // If the path is already clean, do NOT create a redundant journal or touch the registry!
        if (string.Equals(snapshot.RawValue, cleanedPath, StringComparison.Ordinal))
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

        // 2. Write directly to HKCU\Environment (never setx)
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

    public string ComputeCleanedPath(string rawPath)
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

            var normalizedKey = trimmed.TrimEnd('\\', '/').ToUpperInvariant();

            // 1. Filter out duplicates
            if (!seen.Add(normalizedKey))
            {
                continue;
            }

            // 2. Check existence safely:
            // If the path contains ANY unexpanded environment variable (e.g. %USERPROFILE% or C:\tools\%BIN%),
            // it MUST be preserved because it cannot be statically verified without context.
            if (ContainsEnvVariableRegex().IsMatch(trimmed))
            {
                keptEntries.Add(trimmed);
                continue;
            }

            if (!_environmentService.DirectoryExists(trimmed))
            {
                continue;
            }

            keptEntries.Add(trimmed);
        }

        return string.Join(";", keptEntries);
    }

    [GeneratedRegex(@"%[A-Za-z0-9_]+%")]
    private static partial Regex ContainsEnvVariableRegex();
}