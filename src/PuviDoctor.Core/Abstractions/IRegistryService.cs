namespace PuviDoctor.Core.Abstractions;

/// <summary>
/// Domain-level abstraction for Windows registry string value kinds,
/// isolating core domain models from platform-specific namespaces.
/// </summary>
public enum SnapshotRegistryKind
{
    String,
    ExpandString
}

public sealed record RegistryPathSnapshot(
    string? RawValue,
    SnapshotRegistryKind Kind
);

/// <summary>
/// Abstracted registry service for safe, testable Windows environment modifications.
/// </summary>
public interface IRegistryService
{
    /// <summary>
    /// Reads current User PATH from HKCU\Environment preserving value kind.
    /// </summary>
    RegistryPathSnapshot GetUserPathSnapshot();

    /// <summary>
    /// Writes sanitized User PATH directly to HKCU\Environment and broadcasts WM_SETTINGCHANGE.
    /// Never invokes setx.
    /// </summary>
    void SetUserPath(string newPath, SnapshotRegistryKind kind);
}