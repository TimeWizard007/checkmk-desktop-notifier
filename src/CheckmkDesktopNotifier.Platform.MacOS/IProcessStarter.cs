namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Starts a process. Tests supply a fake so URI launch arguments can be asserted
/// without invoking <c>/usr/bin/open</c>.
/// </summary>
public interface IProcessStarter
{
    void Start(string fileName, IReadOnlyList<string> arguments);
}
