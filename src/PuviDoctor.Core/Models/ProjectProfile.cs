namespace PuviDoctor.Core.Models;

/// <summary>
/// A concise, evidence-based description of the project in the target directory.
/// It is used to make diagnostics project-aware before any checks are run.
/// </summary>
public sealed record ProjectProfile(
    IReadOnlyList<string> Types,
    string? PackageManager,
    IReadOnlyList<string> RuntimeRequirements,
    IReadOnlyList<string> AvailableCommands)
{
    public bool IsDetected => Types.Count > 0;
}
