namespace PuviDoctor.Core.Abstractions;

/// <summary>
/// Result of an executed external system command.
/// </summary>
public sealed record ProcessOutput(
    int ExitCode,
    string StandardOutput,
    string StandardError
);

/// <summary>
/// Abstraction for invoking OS commands safely without shell-injection vectors.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Executes a process with strongly typed argument array.
    /// </summary>
    Task<ProcessOutput> ExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default
    );
}