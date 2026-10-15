using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Core.Checks.Path;

/// <summary>
/// Detects multiple installations of key developer tools on the system PATH.
/// Flags instances where an unintended version shadows another.
/// </summary>
public sealed class ShadowingCheck : IHealthCheck
{
    private readonly IEnvironmentService _environmentService;
    private readonly IProcessRunner _processRunner;
    private static readonly string[] MonitoredTools = ["node", "npm", "dotnet", "git"];

    public string Id => "path.shadowing";

    public ShadowingCheck(IEnvironmentService environmentService, IProcessRunner processRunner)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var isWindows = _environmentService.IsWindows();
        var lookupCommand = isWindows ? "where.exe" : "which";
        var shadowedFindings = new List<string>();

        foreach (var tool in MonitoredTools)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var args = isWindows ? new[] { tool } : new[] { "-a", tool };

            try
            {
                var output = await _processRunner.ExecuteAsync(lookupCommand, args, cancellationToken);

                if (output.ExitCode != 0 || string.IsNullOrWhiteSpace(output.StandardOutput))
                {
                    continue; // Tool not found on PATH; handled by version checks
                }

                var hits = output.StandardOutput
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                    .ToList();

                if (hits.Count > 1)
                {
                    shadowedFindings.Add(
                        $"Tool '{tool}' has {hits.Count} instances on PATH. Active winner: '{hits[0]}'. Shadowed: '{string.Join("', '", hits.Skip(1))}'"
                    );
                }
            }
            catch (Exception)
            {
                // Silently bypass lookup tool failures (e.g. where.exe not found on stripped environment)
            }
        }

        if (shadowedFindings.Count == 0)
        {
            return new CheckResult(
                CheckStatus.Ok,
                "No tool shadowing detected for standard runtimes."
            );
        }

        return new CheckResult(
            CheckStatus.Warn,
            $"{shadowedFindings.Count} tool(s) have shadowing duplicates on PATH.",
            string.Join(Environment.NewLine, shadowedFindings.Select(f => $"  - {f}"))
        );
    }
}