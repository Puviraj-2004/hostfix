using PuviDoctor.Core.Abstractions;
using PuviDoctor.Core.Models;

namespace PuviDoctor.Core.Checks.Hygiene;

/// <summary>
/// Audits standard development ports (5173, 5000, 8081) for port collision conflicts.
/// </summary>
public sealed class PortCollisionCheck : IHealthCheck
{
    private readonly INetworkService _networkService;
    private static readonly int[] StandardPorts = [5173, 5000, 8081];

    public string Id => "hygiene.ports";

    public PortCollisionCheck(INetworkService networkService)
    {
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
    }

    public Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken = default)
    {
        var activePorts = _networkService.GetActiveListeningPorts();
        var busyPorts = StandardPorts.Where(p => activePorts.Contains(p)).ToList();

        if (busyPorts.Count == 0)
        {
            return Task.FromResult(new CheckResult(
                CheckStatus.Ok,
                "Standard development ports (5173 Vite, 5000 .NET API, 8081 Expo) are free."
            ));
        }

        var portLabels = busyPorts.Select(p => p switch
        {
            5173 => "5173 (Vite)",
            5000 => "5000 (.NET)",
            8081 => "8081 (Expo)",
            _ => p.ToString()
        });

        return Task.FromResult(new CheckResult(
            CheckStatus.Warn,
            $"{busyPorts.Count} standard dev port(s) currently held by another process: [{string.Join(", ", portLabels)}].",
            "Note: Running 'npm run dev' or 'dotnet run' will fail or re-assign to an unpredictable port unless the holding process is terminated."
        ));
    }
}