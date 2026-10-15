using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Platform;

/// <summary>
/// Production process runner using direct executable invocation without shell wrapper.
/// Automatically resolves Windows batch scripts (.cmd) for node tooling like npm and npx.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessOutput> ExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var resolvedFileName = fileName;

        // Windows quirk: npm, npx, yarn, pnpm are batch scripts (.cmd)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            !resolvedFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            !resolvedFileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) &&
            !resolvedFileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            if (resolvedFileName.Equals("npm", StringComparison.OrdinalIgnoreCase) ||
                resolvedFileName.Equals("npx", StringComparison.OrdinalIgnoreCase) ||
                resolvedFileName.Equals("pnpm", StringComparison.OrdinalIgnoreCase) ||
                resolvedFileName.Equals("yarn", StringComparison.OrdinalIgnoreCase))
            {
                resolvedFileName += ".cmd";
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = resolvedFileName,
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