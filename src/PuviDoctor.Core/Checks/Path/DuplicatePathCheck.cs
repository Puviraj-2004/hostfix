using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Core.Checks.Path;

/// <summary>
/// Identifies redundant directory entries in the PATH variable.
/// Duplicates waste process environment space and can hide path precedence issues.
/// </summary>
public sealed class DuplicatePathCheck : IHealthCheck
{
    private readonly IEnvironmentService _environmentService;

    public string Id => "path.duplicate-entries";

    public DuplicatePathCheck(IEnvironmentService environmentService)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
    }

    public Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var rawPath = _environmentService.GetRawPath();

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return Task.FromResult(new CheckResult(
                CheckStatus.Ok,
                "PATH is empty; no duplicates detected."
            ));
        }

        var isWindows = _environmentService.IsWindows();
        var separator = isWindows ? ';' : ':';
        var comparer = isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        var entries = rawPath.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var seen = new HashSet<string>(comparer);
        var duplicates = new List<string>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = NormalizePath(entry, isWindows);
            if (string.IsNullOrEmpty(normalized))
            {
                continue;
            }

            if (!seen.Add(normalized))
            {
                duplicates.Add(entry.Trim());
            }
        }

        if (duplicates.Count == 0)
        {
            return Task.FromResult(new CheckResult(
                CheckStatus.Ok,
                "No duplicate PATH entries found."
            ));
        }

        var message = $"{duplicates.Count} duplicate entries found in PATH.";
        var details = string.Join(Environment.NewLine, duplicates.Select(d => $"  - {d}"));

        return Task.FromResult(new CheckResult(
            CheckStatus.Warn,
            message,
            details
        ));
    }

    private static string NormalizePath(string path, bool isWindows)
    {
        var trimmed = path.Trim().Trim('"');

        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        // Strip trailing directory separators for accurate comparison
        trimmed = trimmed.TrimEnd('\\', '/');

        return isWindows ? trimmed.ToUpperInvariant() : trimmed;
    }
}