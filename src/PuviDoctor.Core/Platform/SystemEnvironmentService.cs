using System.Runtime.InteropServices;
using PuviDoctor.Core.Abstractions;

namespace PuviDoctor.Core.Platform;

/// <summary>
/// Production wrapper for actual operating system interactions.
/// Isolated here so checks never invoke System.IO or System.Environment directly.
/// </summary>
public sealed class SystemEnvironmentService : IEnvironmentService
{
    public string? GetRawPath()
    {
        return Environment.GetEnvironmentVariable("PATH");
    }

    public bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    public bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            // Expand environment variables if path contains entries like %USERPROFILE% or $HOME
            var expanded = Environment.ExpandEnvironmentVariables(path);
            return Directory.Exists(expanded);
        }
        catch (Exception)
        {
            // Invalid characters, security restrictions, or corrupt path segment
            return false;
        }
    }
}