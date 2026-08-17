using System.Xml.Linq;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class LocalizationKeyTests
{
    [Fact]
    public void Phase4a_strings_exist_in_english_and_polish()
    {
        var root = FindRepoRoot();
        var en = LoadKeys(Path.Combine(root, "src/CheckmkDesktopNotifier.App/Localization/Strings.resx"));
        var pl = LoadKeys(Path.Combine(root, "src/CheckmkDesktopNotifier.App/Localization/Strings.pl.resx"));
        string[] required =
        [
            "ConnectionInitializing",
            "MenuConnectionSettings",
            "MenuHelpAbout",
            "MenuExit",
            "MenuOpen",
            "MenuMuteSound",
            "MenuUnmuteSound",
            "MenuHideToTray",
            "TestNotificationSound",
            "SettingsTabConnection",
            "SettingsTabNotifications",
            "FilterAll",
            "FilterNew",
            "FilterCritical",
            "FilterWarning",
            "FilterUnknown",
            "EmptyFilterNew",
            "EmptyFilterCritical",
            "SoundSection",
            "SoundDefault",
            "SoundCustomWav",
            "SoundChooseWav",
            "SoundVolume",
            "SoundRestoreDefault",
            "SoundMute",
            "AboutTitle",
            "AboutDescription",
            "AboutVersion",
            "AboutAuthor",
            "AboutGitHub"
        ];

        foreach (var key in required)
        {
            Assert.Contains(key, en);
            Assert.Contains(key, pl);
            Assert.False(string.IsNullOrWhiteSpace(en[key]));
            Assert.False(string.IsNullOrWhiteSpace(pl[key]));
        }

        Assert.Equal("Initializing...", en["ConnectionInitializing"]);
        Assert.Equal("Uruchamianie...", pl["ConnectionInitializing"]);
        Assert.Equal("Connection settings", en["MenuConnectionSettings"]);
        Assert.Equal("Ustawienia połączenia", pl["MenuConnectionSettings"]);
        Assert.Equal("Help / About", en["MenuHelpAbout"]);
        Assert.Equal("Pomoc / O programie", pl["MenuHelpAbout"]);
        Assert.Equal("Exit", en["MenuExit"]);
        Assert.Equal("Zakończ", pl["MenuExit"]);
        Assert.Equal("Open", en["MenuOpen"]);
        Assert.Equal("Otwórz", pl["MenuOpen"]);
        Assert.Equal("Mute sound", en["MenuMuteSound"]);
        Assert.Equal("Wycisz dźwięk", pl["MenuMuteSound"]);
        Assert.Equal("Unmute sound", en["MenuUnmuteSound"]);
        Assert.Equal("Włącz dźwięk", pl["MenuUnmuteSound"]);
        Assert.Equal("Hide to tray", en["MenuHideToTray"]);
        Assert.Equal("Ukryj do zasobnika", pl["MenuHideToTray"]);
        Assert.Equal("Test notification sound", en["TestNotificationSound"]);
        Assert.Equal("Testuj dźwięk powiadomienia", pl["TestNotificationSound"]);
        Assert.Equal("Connection", en["SettingsTabConnection"]);
        Assert.Equal("Połączenie", pl["SettingsTabConnection"]);
        Assert.Equal("Notifications", en["SettingsTabNotifications"]);
        Assert.Equal("Powiadomienia", pl["SettingsTabNotifications"]);
        Assert.Equal("ALL", en["FilterAll"]);
        Assert.Equal("WSZYSTKIE", pl["FilterAll"]);
        Assert.Equal("NEW", en["FilterNew"]);
        Assert.Equal("NOWE", pl["FilterNew"]);
        Assert.Equal("CRIT", en["FilterCritical"]);
        Assert.Equal("CRIT", pl["FilterCritical"]);
        Assert.Equal("WARN", en["FilterWarning"]);
        Assert.Equal("WARN", pl["FilterWarning"]);
        Assert.Equal("UNK", en["FilterUnknown"]);
        Assert.Equal("NIEZNANE", pl["FilterUnknown"]);
        Assert.Equal("No new problems.", en["EmptyFilterNew"]);
        Assert.Equal("Brak nowych problemów.", pl["EmptyFilterNew"]);
        Assert.Equal("No critical problems.", en["EmptyFilterCritical"]);
        Assert.Equal("Brak problemów krytycznych.", pl["EmptyFilterCritical"]);
        Assert.Equal("Sound", en["SoundSection"]);
        Assert.Equal("Dźwięk", pl["SoundSection"]);
        Assert.Equal("Default notifier sound", en["SoundDefault"]);
        Assert.Equal("Domyślny dźwięk powiadomienia", pl["SoundDefault"]);
        Assert.Equal("Custom WAV", en["SoundCustomWav"]);
        Assert.Equal("Własny WAV", pl["SoundCustomWav"]);
        Assert.Equal("Choose WAV...", en["SoundChooseWav"]);
        Assert.Equal("Wybierz WAV...", pl["SoundChooseWav"]);
        Assert.Equal("Volume", en["SoundVolume"]);
        Assert.Equal("Głośność", pl["SoundVolume"]);
        Assert.Equal("Restore default sound", en["SoundRestoreDefault"]);
        Assert.Equal("Przywróć domyślny dźwięk", pl["SoundRestoreDefault"]);
        Assert.Equal("Mute sound", en["SoundMute"]);
        Assert.Equal("Wycisz dźwięk", pl["SoundMute"]);
    }

    private static Dictionary<string, string> LoadKeys(string path)
    {
        Assert.True(File.Exists(path), path);
        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .ToDictionary(
                e => (string)e.Attribute("name")!,
                e => (e.Element("value")?.Value ?? string.Empty).Trim(),
                StringComparer.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CheckmkDesktopNotifier.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
