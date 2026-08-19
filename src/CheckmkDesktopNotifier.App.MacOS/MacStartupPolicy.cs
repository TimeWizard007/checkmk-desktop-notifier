namespace CheckmkDesktopNotifier.App.MacOS;

public static class MacStartupPolicy
{
    public static bool ShowSettingsOnStartup(bool needsFirstRunSetup) => needsFirstRunSetup;

    public static bool StartPollingOnStartup(bool isUsableReal) => isUsableReal;
}
