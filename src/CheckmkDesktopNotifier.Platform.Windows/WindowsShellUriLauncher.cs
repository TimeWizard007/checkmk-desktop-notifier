using System.Diagnostics;
using CheckmkDesktopNotifier.Core.Navigation;

namespace CheckmkDesktopNotifier.Platform.Windows;

/// <summary>
/// Opens a URI with the Windows shell (default browser). Same behavior as v1.2.0.
/// </summary>
public sealed class WindowsShellUriLauncher : IUriLauncher
{
    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }
}
