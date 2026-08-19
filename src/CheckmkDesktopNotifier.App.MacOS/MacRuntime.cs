using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS;

internal static class MacRuntime
{
    public static MacSingleInstanceLock? SingleInstance { get; set; }
}
