using CheckmkDesktopNotifier.Core;

namespace CheckmkDesktopNotifier.Platform.Windows;

/// <summary>
/// Windows install layout under <c>%LocalAppData%\Programs\CheckmkDesktopNotifier</c>.
/// </summary>
public static class WindowsInstallLayout
{
    public static string LocalApplicationData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string GetPerUserInstallDirectory() =>
        InstallLayout.GetPerUserInstallDirectory(LocalApplicationData);

    public static string GetPerUserInstallExecutablePath() =>
        InstallLayout.GetPerUserInstallExecutablePath(LocalApplicationData);

    public static string GetPerUserDataDirectory() =>
        InstallLayout.GetPerUserDataDirectory(LocalApplicationData);

    public static bool BinariesAreSeparateFromUserData() =>
        InstallLayout.BinariesAreSeparateFromUserData(LocalApplicationData);
}
