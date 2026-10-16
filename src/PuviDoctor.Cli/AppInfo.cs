using System.Reflection;

namespace PuviDoctor.Cli;

internal static class AppInfo
{
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = typeof(AppInfo).Assembly;
        var value = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? assembly.GetName().Version?.ToString(3)
                    ?? "0.0.0";
        var metadataIndex = value.IndexOf('+');
        return metadataIndex >= 0 ? value[..metadataIndex] : value;
    }
}
