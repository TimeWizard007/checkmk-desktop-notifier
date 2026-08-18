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
            "SettingsTabGeneral",
            "SettingsStartWithWindows",
            "SettingsAutostartFailed",
            "FilterAll",
            "FilterNew",
            "FilterCritical",
            "FilterWarning",
            "FilterUnknown",
            "FilterTaken",
            "TakenLabel",
            "SearchPlaceholder",
            "EmptyFilterTaken",
            "EmptyFilterSearch",
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
            "AboutGitHub",
            "Take",
            "Taking",
            "Taken",
            "TakenByFormat",
            "EnableTake",
            "DisplayName",
            "TeamCoordination",
            "TakeConfirmTitle",
            "TakeCouldNot",
            "TakeForbidden",
            "OpenInCheckmk",
            "MarkAsSeen",
            "MarkAsUnseen"
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
        Assert.Equal("General", en["SettingsTabGeneral"]);
        Assert.Equal("Ogólne", pl["SettingsTabGeneral"]);
        Assert.Equal("Start with Windows", en["SettingsStartWithWindows"]);
        Assert.Equal("Uruchamiaj z systemem Windows", pl["SettingsStartWithWindows"]);
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
        Assert.Equal("TAKEN", en["FilterTaken"]);
        Assert.Equal("PRZEJĘTE", pl["FilterTaken"]);
        Assert.Equal("TAKEN", en["TakenLabel"]);
        Assert.Equal("PRZEJ.", pl["TakenLabel"]);
        Assert.Equal("Search host or service...", en["SearchPlaceholder"]);
        Assert.Equal("Szukaj hosta lub usługi...", pl["SearchPlaceholder"]);
        Assert.Equal("No taken problems.", en["EmptyFilterTaken"]);
        Assert.Equal("Brak przejętych problemów.", pl["EmptyFilterTaken"]);
        Assert.Equal("No matching problems.", en["EmptyFilterSearch"]);
        Assert.Equal("Brak pasujących problemów.", pl["EmptyFilterSearch"]);
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
        Assert.Equal("Take", en["Take"]);
        Assert.Equal("Przejmij", pl["Take"]);
        Assert.Equal("Taking...", en["Taking"]);
        Assert.Equal("Przejmowanie...", pl["Taking"]);
        Assert.Equal("Taken", en["Taken"]);
        Assert.Equal("Przejęte", pl["Taken"]);
        Assert.Equal("Taken by {0}", en["TakenByFormat"]);
        Assert.Equal("Przejęte przez {0}", pl["TakenByFormat"]);
        Assert.Equal("Enable Take / Acknowledge in Checkmk", en["EnableTake"]);
        Assert.Equal("Włącz Przejmij / ACK w Checkmk", pl["EnableTake"]);
        Assert.Equal("Display name", en["DisplayName"]);
        Assert.Equal("Nazwa wyświetlana", pl["DisplayName"]);
        Assert.Equal("Team coordination", en["TeamCoordination"]);
        Assert.Equal("Koordynacja zespołu", pl["TeamCoordination"]);
        Assert.Equal("Take this problem?", en["TakeConfirmTitle"]);
        Assert.Equal("Przejąć ten problem?", pl["TakeConfirmTitle"]);
        Assert.Equal("Could not acknowledge the problem.", en["TakeCouldNot"]);
        Assert.Equal("Nie udało się potwierdzić problemu w Checkmk.", pl["TakeCouldNot"]);
        Assert.Equal("This Checkmk account cannot acknowledge problems.", en["TakeForbidden"]);
        Assert.Equal("To konto Checkmk nie ma uprawnień do potwierdzania problemów.", pl["TakeForbidden"]);
        Assert.Equal("Open in Checkmk", en["OpenInCheckmk"]);
        Assert.Equal("Otwórz w Checkmk", pl["OpenInCheckmk"]);
        Assert.Equal("Mark seen", en["MarkAsSeen"]);
        Assert.Equal("Oznacz jako przeczytane", pl["MarkAsSeen"]);
        Assert.Equal("Mark unseen", en["MarkAsUnseen"]);
        Assert.Equal("Oznacz jako nieprzeczytane", pl["MarkAsUnseen"]);
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
