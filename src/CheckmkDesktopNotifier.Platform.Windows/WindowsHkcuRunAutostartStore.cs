using Microsoft.Win32;
using CheckmkDesktopNotifier.Core.Autostart;

namespace CheckmkDesktopNotifier.Platform.Windows;

/// <summary>
/// Per-user HKCU Run entry. No HKLM, no scheduled task, no elevation.
/// </summary>
public sealed class WindowsHkcuRunAutostartStore : IAutostartStore
{
    public AutostartRegistration? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutostartCommand.SubKey, writable: false);
        var value = key?.GetValue(AutostartCommand.ValueName) as string;
        return string.IsNullOrWhiteSpace(value) ? null : new AutostartRegistration(value);
    }

    public void Write(AutostartRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        using var key = Registry.CurrentUser.CreateSubKey(AutostartCommand.SubKey, writable: true);
        key.SetValue(AutostartCommand.ValueName, registration.CommandLine, RegistryValueKind.String);
    }

    public void Delete()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutostartCommand.SubKey, writable: true);
        key?.DeleteValue(AutostartCommand.ValueName, throwOnMissingValue: false);
    }
}
