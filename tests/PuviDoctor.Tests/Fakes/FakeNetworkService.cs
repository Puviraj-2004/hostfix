using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Tests.Fakes;

public sealed class FakeNetworkService : INetworkService
{
    public HashSet<int> BusyPorts { get; } = [];

    public IReadOnlySet<int> GetActiveListeningPorts() => BusyPorts;
}