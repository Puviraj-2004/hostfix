namespace PuviDoctor.Core.Abstractions;

/// <summary>
/// Execution context injected into each check, providing target workspace path and flags.
/// </summary>
public sealed record CheckContext(
    string ProjectRootDirectory,
    bool IsOfflineMode = false
);