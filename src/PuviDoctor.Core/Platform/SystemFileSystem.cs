using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Platform;

/// <summary>
/// Real file system implementation using System.IO.
/// </summary>
public sealed class SystemFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<string> GetFiles(string directory, string searchPattern) =>
        Directory.Exists(directory) ? Directory.EnumerateFiles(directory, searchPattern) : [];

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        return File.ReadAllTextAsync(path, cancellationToken);
    }
}
