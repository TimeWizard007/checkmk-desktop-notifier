namespace CheckmkDesktopNotifier.Core.Navigation;

/// <summary>
/// Opens a URI in the platform default handler. Windows uses shell execute;
/// macOS will use NSWorkspace / <c>open</c>.
/// </summary>
public interface IUriLauncher
{
    void Open(Uri uri);
}
