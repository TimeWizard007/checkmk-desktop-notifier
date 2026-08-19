namespace CheckmkDesktopNotifier.Platform.MacOS;

public static class MacStatusItemFactory
{
    public static IMacStatusItem Create()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new NativeMacStatusItem();
        }

        return new NullMacStatusItem();
    }
}
