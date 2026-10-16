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

    /// <summary>Executes a command from a project directory without changing the caller process directory.</summary>
    Task<ProcessOutput> ExecuteInDirectoryAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(fileName, arguments, cancellationToken);
}
