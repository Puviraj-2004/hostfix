using System.Net.NetworkInformation;
using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Platform;

public sealed class SystemNetworkService : INetworkService
{
    public IReadOnlySet<int> GetActiveListeningPorts()
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var listeners = properties.GetActiveTcpListeners();
        return listeners.Select(l => l.Port).ToHashSet();
    }
}