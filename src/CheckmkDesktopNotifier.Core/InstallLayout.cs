namespace CheckmkDesktopNotifier.Core;

/// <summary>
/// Installed binaries and per-user data are siblings under LocalAppData, never the same folder.
/// </summary>
public static class InstallLayout
{
    public const string ProgramsFolderName = "Programs";

    public const string ApplicationFolderName = "CheckmkDesktopNotifier";

    public const string ExecutableFileName = "CheckmkDesktopNotifier.exe";

    public static string GetPerUserInstallDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProgramsFolderName,
            ApplicationFolderName);

    public static string GetPerUserInstallExecutablePath() =>
        Path.Combine(GetPerUserInstallDirectory(), ExecutableFileName);

    public static string GetPerUserDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationFolderName);

    public static bool BinariesAreSeparateFromUserData()
    {
        var install = Path.GetFullPath(GetPerUserInstallDirectory());
        var data = Path.GetFullPath(GetPerUserDataDirectory());
        return !string.Equals(install, data, StringComparison.OrdinalIgnoreCase)
            && !IsUnder(install, data)
            && !IsUnder(data, install);
    }

    private static bool IsUnder(string path, string parent)
    {
        var prefix = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
