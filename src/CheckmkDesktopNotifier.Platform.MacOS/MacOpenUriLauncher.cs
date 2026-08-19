using CheckmkDesktopNotifier.Core.Navigation;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Opens a URI with the macOS default handler via <c>/usr/bin/open</c>.
/// Launch failures must not crash the host.
/// </summary>
public sealed class MacOpenUriLauncher : IUriLauncher
{
    private readonly IProcessStarter _process;

    public MacOpenUriLauncher()
        : this(new MacProcessStarter())
    {
    }

    public MacOpenUriLauncher(IProcessStarter process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        try
        {
            var arguments = MacOpenCommand.BuildArguments(uri);
            _process.Start(MacOpenCommand.Executable, arguments);
        }
        catch (Exception)
        {
            // Default-browser launch is best-effort. Never crash the host.
        }
    }
}
