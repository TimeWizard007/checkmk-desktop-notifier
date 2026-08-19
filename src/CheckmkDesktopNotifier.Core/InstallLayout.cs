namespace CheckmkDesktopNotifier.Core;

/// <summary>
/// Windows per-user install vs data folder layout under a LocalAppData root.
/// Callers supply the root; Core does not read OS special folders.
/// </summary>
public static class InstallLayout
{
    public const string ProgramsFolderName = "Programs";

    public const string ApplicationFolderName = "CheckmkDesktopNotifier";

    public const string ExecutableFileName = "CheckmkDesktopNotifier.exe";

    public static string GetPerUserInstallDirectory(string localApplicationDataRoot) =>
        Path.Combine(RequireRoot(localApplicationDataRoot), ProgramsFolderName, ApplicationFolderName);

    public static string GetPerUserInstallExecutablePath(string localApplicationDataRoot) =>
        Path.Combine(GetPerUserInstallDirectory(localApplicationDataRoot), ExecutableFileName);

    public static string GetPerUserDataDirectory(string localApplicationDataRoot) =>
        Path.Combine(RequireRoot(localApplicationDataRoot), ApplicationFolderName);

    public static bool BinariesAreSeparateFromUserData(string localApplicationDataRoot)
    {
        var install = Path.GetFullPath(GetPerUserInstallDirectory(localApplicationDataRoot));
        var data = Path.GetFullPath(GetPerUserDataDirectory(localApplicationDataRoot));
        return !string.Equals(install, data, StringComparison.OrdinalIgnoreCase)
            && !IsUnder(install, data)
            && !IsUnder(data, install);
    }

    private static string RequireRoot(string localApplicationDataRoot)
    {
        if (string.IsNullOrWhiteSpace(localApplicationDataRoot))
        {
            throw new ArgumentException("Local application data root must not be empty.", nameof(localApplicationDataRoot));
        }

        return localApplicationDataRoot;
    }

    private static bool IsUnder(string path, string parent)
    {
        var prefix = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
