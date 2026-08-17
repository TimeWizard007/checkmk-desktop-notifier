using System.Diagnostics;

namespace CheckmkDesktopNotifier.App;

public sealed class ShellUriLauncher : IUriLauncher
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
