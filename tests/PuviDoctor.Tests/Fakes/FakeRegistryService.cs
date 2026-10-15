using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Tests.Fakes;

public sealed class FakeRegistryService : IRegistryService
{
    public string? CurrentPath { get; set; }
    public SnapshotRegistryKind CurrentKind { get; set; } = SnapshotRegistryKind.ExpandString;

    public RegistryPathSnapshot GetUserPathSnapshot()
    {
        return new RegistryPathSnapshot(CurrentPath, CurrentKind);
    }

    public void SetUserPath(string newPath, SnapshotRegistryKind kind)
    {
        CurrentPath = newPath;
        CurrentKind = kind;
    }
}