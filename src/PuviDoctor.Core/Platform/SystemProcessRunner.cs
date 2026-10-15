using System.Diagnostics;
using System.Text;
using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Platform;

/// <summary>
/// Production process runner using direct executable invocation without shell wrapper.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessOutput> ExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessOutput(
            process.ExitCode,
            outputBuilder.ToString().TrimEnd(),
            errorBuilder.ToString().TrimEnd()
        );
    }
}