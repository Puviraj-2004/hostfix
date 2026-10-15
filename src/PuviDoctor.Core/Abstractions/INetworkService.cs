namespace PuviDoctor.Core.Abstractions;

public interface INetworkService
{
    /// <summary>
    /// Returns list of active TCP listening port numbers on localhost.
    /// </summary>
    IReadOnlySet<int> GetActiveListeningPorts();
}