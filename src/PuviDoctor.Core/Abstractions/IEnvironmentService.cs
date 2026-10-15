namespace PuviDoctor.Core.Abstractions;

/// <summary>
/// Wraps access to environment variables and platform details.
/// Abstracted to enable unit test execution on synthetic environments without host tampering.
/// </summary>
public interface IEnvironmentService
{
    /// <summary>
    /// Retrieves raw PATH environment variable value.
    /// </summary>
    string? GetRawPath();

    /// <summary>
    /// Identifies the runtime operating system platform.
    /// </summary>
    bool IsWindows();

    /// <summary>
    /// Checks if a folder path actually exists on disk.
    /// </summary>
    bool DirectoryExists(string path);
}