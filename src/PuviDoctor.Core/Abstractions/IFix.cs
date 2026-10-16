namespace PuviDoctor.Core.Abstractions;

/// <summary>
/// Defines a safe, reversible mutation operation.
/// Every fix must describe what it does, provide a dry-run diff, and support rollback.
/// </summary>
public interface IFix
{
    /// <summary>
    /// True only when this fix would make a real change to the machine.
    /// </summary>
    bool CanApply();

    /// <summary>
    /// Human-readable explanation shown before anything mutates.
    /// </summary>
    string Describe();

    /// <summary>
    /// Generates exact before-and-after diff without touching the machine.
    /// </summary>
    string DryRun();

    /// <summary>
    /// Applies the fix. Implementation must record rollback journal first.
    /// </summary>
    Task ApplyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores the exact prior state from the journal.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
