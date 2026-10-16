using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Tests.Fakes;

/// <summary>
/// In-memory file system fake for testing configuration files.
/// </summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public void AddFile(string path, string content)
    {
        _files[Path.GetFullPath(path)] = content;
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(Path.GetFullPath(path));
    }

    public IEnumerable<string> GetFiles(string directory, string searchPattern)
    {
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var suffix = searchPattern.StartsWith('*') ? searchPattern[1..] : searchPattern;

        return _files.Keys.Where(path =>
            string.Equals(Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), fullDirectory, StringComparison.OrdinalIgnoreCase) &&
            (searchPattern == "*" || Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        if (_files.TryGetValue(fullPath, out var content))
        {
            return Task.FromResult(content);
        }

        throw new FileNotFoundException($"Fake file not found: {path}");
    }
}
