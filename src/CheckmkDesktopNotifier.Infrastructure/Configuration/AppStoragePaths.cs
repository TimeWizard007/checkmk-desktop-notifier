using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public sealed class AppStoragePaths
{
    public const string ApplicationFolderName = "CheckmkDesktopNotifier";
    public const string SettingsFileName = "settings.json";
    public const string LegacyAlertStateFileName = "alert-state.json";
    public const string LastPollFileName = "last-poll.txt";

    public AppStoragePaths(string appDataDirectory)
    {
        if (string.IsNullOrWhiteSpace(appDataDirectory))
        {
            throw new ArgumentException("App data directory must not be empty.", nameof(appDataDirectory));
        }

        AppDataDirectory = appDataDirectory;
    }

    public string AppDataDirectory { get; }

    public string SettingsPath => Path.Combine(AppDataDirectory, SettingsFileName);

    public string LegacyAlertStatePath => Path.Combine(AppDataDirectory, LegacyAlertStateFileName);

    public string LastPollPath => Path.Combine(AppDataDirectory, LastPollFileName);

    public string AlertStatePathFor(ConnectionIdentity identity) =>
        Path.Combine(AppDataDirectory, "state", identity.FileId, LegacyAlertStateFileName);

    public static AppStoragePaths ForCurrentUser()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationFolderName);
        Directory.CreateDirectory(appData);
        return new AppStoragePaths(appData);
    }
}
