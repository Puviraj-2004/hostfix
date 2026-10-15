using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Models;

public sealed record CheckExecutionResult(
    IHealthCheck Check,
    CheckResult Result
);

public sealed record HealthReport(
    IReadOnlyList<CheckExecutionResult> Executions,
    int Score,
    int MaxScore,
    int ExitCode
);