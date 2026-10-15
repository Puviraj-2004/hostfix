using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Tests.Fakes;

/// <summary>
/// Deterministic mock for process execution.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessOutput> _responses = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterOutput(string commandWithArgs, int exitCode, string stdout, string stderr = "")
    {
        _responses[commandWithArgs] = new ProcessOutput(exitCode, stdout, stderr);
    }

    public Task<ProcessOutput> ExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var key = $"{fileName} {string.Join(" ", arguments)}".Trim();

        if (_responses.TryGetValue(key, out var output))
        {
            return Task.FromResult(output);
        }

        return Task.FromResult(new ProcessOutput(1, string.Empty, "Command not found"));
    }
}