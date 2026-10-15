using PuviDoctor.Core.Models;

namespace PuviDoctor.Core.Abstractions;

/// <summary>
/// Contract implemented by all health checks in the engine.
/// A check must be read-only and never mutate host environment directly.
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Stable, documented unique identifier (e.g., "path.dead-entries", "version.node").
    /// Used for CLI filtering, config ignore/severity rules, and docs references.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Evaluates the check against provided context.
    /// </summary>
    Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default);
}