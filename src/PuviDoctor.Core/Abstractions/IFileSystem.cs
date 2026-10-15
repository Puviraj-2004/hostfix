namespace PuviDoctor.Core.Abstractions;

/// <summary>
/// Abstraction for disk access to allow testing checks against synthetic project roots.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
}