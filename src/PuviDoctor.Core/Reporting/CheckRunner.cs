using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Core.Reporting;

/// <summary>
/// Orchestrates the execution of all registered health checks and computes overall health score and exit code.
/// </summary>
public sealed class CheckRunner
{
    private readonly IEnumerable<IHealthCheck> _checks;

    public CheckRunner(IEnumerable<IHealthCheck> checks)
    {
        _checks = checks ?? throw new ArgumentNullException(nameof(checks));
    }

    public async Task<HealthReport> RunAllAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var executions = new List<CheckExecutionResult>();

        foreach (var check in _checks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await check.RunAsync(context, cancellationToken);
                executions.Add(new CheckExecutionResult(check, result));
            }
            catch (Exception ex)
            {
                // Isolate check bugs from bringing down runner
                executions.Add(new CheckExecutionResult(
                    check,
                    new CheckResult(CheckStatus.Fail, $"Check threw unhandled exception: {ex.Message}", ex.ToString())
                ));
            }
        }

        // Calculate exit code
        // 2: At least one Fail
        // 1: Warnings only (no Fails)
        // 0: Everything Ok or Skip
        int exitCode = 0;
        if (executions.Any(e => e.Result.Status == CheckStatus.Fail))
        {
            exitCode = 2;
        }
        else if (executions.Any(e => e.Result.Status == CheckStatus.Warn))
        {
            exitCode = 1;
        }

        // Calculate score (out of 10)
        var scorableExecutions = executions.Where(e => e.Result.Status != CheckStatus.Skip).ToList();
        int score = 10;

        if (scorableExecutions.Count > 0)
        {
            var passedCount = scorableExecutions.Count(e => e.Result.Status == CheckStatus.Ok);
            score = (int)Math.Round((double)passedCount / scorableExecutions.Count * 10);
        }

        return new HealthReport(executions, score, 10, exitCode);
    }
}