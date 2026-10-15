using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Models;

/// <summary>
/// Represents the result returned by an IHealthCheck.
/// </summary>
/// <param name="Status">Ok, Warn, Fail, or Skip.</param>
/// <param name="Message">Single line summary for terminal output.</param>
/// <param name="Details">Optional multi-line context or trace.</param>
/// <param name="Fix">Optional automated fix if issue is resolvable safely.</param>
public sealed record CheckResult(
    CheckStatus Status,
    string Message,
    string? Details = null,
    IFix? Fix = null
);