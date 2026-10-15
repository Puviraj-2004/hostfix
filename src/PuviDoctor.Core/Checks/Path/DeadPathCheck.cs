using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Fixes;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Core.Checks.Path;

/// <summary>
/// Checks whether entries defined in the PATH environment variable actually exist on disk.
/// Non-existent entries degrade shell lookup performance and cause silent tool resolution failures.
/// </summary>
public sealed class DeadPathCheck : IHealthCheck
{
    private readonly IEnvironmentService _environmentService;
    private readonly IRegistryService _registryService;

    public string Id => "path.dead-entries";

    public DeadPathCheck(IEnvironmentService environmentService, IRegistryService registryService)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
    }

    public Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var rawPath = _environmentService.GetRawPath();

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return Task.FromResult(new CheckResult(
                CheckStatus.Warn,
                "PATH environment variable is empty or unset."
            ));
        }

        char pathSeparator = _environmentService.IsWindows() ? ';' : ':';
        var entries = rawPath.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var deadEntries = new List<string>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cleanEntry = entry.Trim().Trim('\"');

            if (string.IsNullOrWhiteSpace(cleanEntry))
            {
                continue;
            }

            if (!_environmentService.DirectoryExists(cleanEntry))
            {
                deadEntries.Add(cleanEntry);
            }
        }

        if (deadEntries.Count == 0)
        {
            return Task.FromResult(new CheckResult(
                CheckStatus.Ok,
                "All PATH directories exist on disk."
            ));
        }

        var message = $"{deadEntries.Count} entries in PATH point to directories that do not exist.";
        var details = string.Join(Environment.NewLine, deadEntries.Select(d => $"  - {d}"));

        IFix? fix = _environmentService.IsWindows()
            ? new WindowsPathFix(_registryService, _environmentService, Id)
            : null;

        return Task.FromResult(new CheckResult(
            CheckStatus.Warn,
            message,
            details,
            fix
        ));
    }
}