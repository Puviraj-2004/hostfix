using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Models;

/// <summary>
/// Stores immutable snapshots of machine state before any modification occurs.
/// Written to disk before mutation; used for 100% deterministic rollback.
/// </summary>
public sealed record FixSessionJournal(
    string SessionId,
    DateTimeOffset TimestampUtc,
    string CheckId,
    string Description,
    RegistryPathSnapshot BeforeUserPathSnapshot
);