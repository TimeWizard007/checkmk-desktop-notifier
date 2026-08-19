using CheckmkDesktopNotifier.Core.Storage;

namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public sealed class AppStoragePaths
{
    public const string ApplicationFolderName = "CheckmkDesktopNotifier";
    public const string SettingsFileName = "settings.json";
    public const string PreferencesFileName = "preferences.json";
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

    public string PreferencesPath => Path.Combine(AppDataDirectory, PreferencesFileName);

    public string CustomNotificationSoundPath =>
        Path.Combine(AppDataDirectory, "assets", "custom-notification.wav");

    public string LegacyAlertStatePath => Path.Combine(AppDataDirectory, LegacyAlertStateFileName);

    public string LastPollPath => Path.Combine(AppDataDirectory, LastPollFileName);

    public string AlertStatePathFor(ConnectionIdentity identity) =>
        Path.Combine(AppDataDirectory, "state", identity.FileId, LegacyAlertStateFileName);

    /// <summary>
    /// Builds paths under a platform-supplied user-data directory. Does not read OS special folders.
    /// </summary>
    public static AppStoragePaths For(IUserDataDirectory userData)
    {
        ArgumentNullException.ThrowIfNull(userData);
        var appData = userData.GetDirectory();
        if (string.IsNullOrWhiteSpace(appData))
        {
            throw new InvalidOperationException("User data directory must not be empty.");
        }

        Directory.CreateDirectory(appData);
        return new AppStoragePaths(appData);
    }
}
