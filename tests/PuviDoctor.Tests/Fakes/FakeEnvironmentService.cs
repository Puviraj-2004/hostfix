using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Tests.Fakes;

/// <summary>
/// Fake environment service allowing deterministic test setups for PATH validation.
/// </summary>
public sealed class FakeEnvironmentService : IEnvironmentService
{
    public string? RawPath { get; set; }
    public bool Windows { get; set; } = true;
    public HashSet<string> ExistingDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetRawPath() => RawPath;

    public bool IsWindows() => Windows;

    public bool DirectoryExists(string path)
    {
        var clean = path.Trim().Trim('\"');
        return ExistingDirectories.Contains(clean);
    }
}