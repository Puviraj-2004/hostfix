namespace PuviDoctor.Core.Models;

/// <summary>
/// Status outcome for an individual check execution.
/// </summary>
public enum CheckStatus
{
    Ok,
    Warn,
    Fail,
    Skip
}