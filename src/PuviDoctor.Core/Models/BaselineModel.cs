namespace PuviDoctor.Core.Models;

public sealed record BaselineEntry(
    string CheckId,
    string Status,
    string Message
);

public sealed record PuviDoctorBaseline(
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<BaselineEntry> AcceptedFindings
);