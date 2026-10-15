using System.Runtime.InteropServices;
using Microsoft.Win32;
using PuviDoctor.Core.Abstractions;
using Win32RegistryValueKind = Microsoft.Win32.RegistryValueKind;

namespace PuviDoctor.Core.Platform.Windows;

public sealed class SystemRegistryService : IRegistryService
{
    private const string EnvironmentSubKey = "Environment";
    private const string PathValueName = "Path";

    public RegistryPathSnapshot GetUserPathSnapshot()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new RegistryPathSnapshot(null, SnapshotRegistryKind.String);
        }

        using var key = Registry.CurrentUser.OpenSubKey(EnvironmentSubKey, writable: false);
        if (key is null)
        {
            return new RegistryPathSnapshot(null, SnapshotRegistryKind.String);
        }

        var value = key.GetValue(PathValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        var winKind = key.GetValueKind(PathValueName);

        var domainKind = winKind == Win32RegistryValueKind.ExpandString
            ? SnapshotRegistryKind.ExpandString
            : SnapshotRegistryKind.String;

        return new RegistryPathSnapshot(value, domainKind);
    }

    public void SetUserPath(string newPath, SnapshotRegistryKind kind)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Direct registry modifications are only supported on Windows.");
        }

        var winKind = kind == SnapshotRegistryKind.ExpandString
            ? Win32RegistryValueKind.ExpandString
            : Win32RegistryValueKind.String;

        using (var key = Registry.CurrentUser.OpenSubKey(EnvironmentSubKey, writable: true))
        {
            if (key is null)
            {
                throw new InvalidOperationException("Could not open HKCU\\Environment for writing.");
            }

            key.SetValue(PathValueName, newPath, winKind);
        }

        // Broadcast WM_SETTINGCHANGE so running explorer & shells detect change without reboot
        BroadcastSettingChange();
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult
    );

    private const int HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private static void BroadcastSettingChange()
    {
        try
        {
            SendMessageTimeout(
                (IntPtr)HWND_BROADCAST,
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "Environment",
                SMTO_ABORTIFHUNG,
                2000,
                out _
            );
        }
        catch
        {
            // Non-critical: manual shell restart will still pick it up
        }
    }
}