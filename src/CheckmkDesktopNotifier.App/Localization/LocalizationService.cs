using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace CheckmkDesktopNotifier.App.Localization;

public sealed class LocalizationService : ILocalizationService, INotifyPropertyChanged
{
    private static readonly ResourceManager ResourceManager = new(
        "CheckmkDesktopNotifier.App.Localization.Strings",
        typeof(LocalizationService).Assembly);

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture => _culture;

    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        _culture = culture;
        CultureInfo.CurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public string CompactBarTitle => Get();
    public string NewLabel => Get();
    public string CriticalLabel => Get();
    public string WarningLabel => Get();
    public string UnknownLabel => Get();
    public string LastCheckLabel => Get();
    public string LastCheckUnknown => Get();
    public string MarkAllNewAsSeen => Get();
    public string MarkAsSeen => Get();
    public string MarkAsUnseen => Get();
    public string OpenInCheckmk => Get();
    public string Seen => Get();
    public string Acknowledged => Get();
    public string AcknowledgedTooltip => Get();
    public string Downtime => Get();
    public string DowntimeTooltip => Get();
    public string HostKind => Get();
    public string NewSection => Get();
    public string CriticalSection => Get();
    public string WarningSection => Get();
    public string UnknownSection => Get();
    public string NoNewProblems => Get();
    public string NoProblems => Get();
    public string ProblemListTitle => Get();
    public string SeverityCritical => Get();
    public string SeverityWarning => Get();
    public string SeverityUnknown => Get();
    public string ConnectionConnected => Get();
    public string ConnectionRefreshing => Get();
    public string ConnectionError => Get();
    public string ConnectionSetupRequired => Get();
    public string ConnectionInitializing => Get();
    public string Settings => Get();
    public string SettingsTitle => Get();
    public string SettingsIntro => Get();
    public string SettingsServerUrl => Get();
    public string SettingsSite => Get();
    public string SettingsUsername => Get();
    public string SettingsSecret => Get();
    public string SettingsSecretHint => Get();
    public string SettingsPollInterval => Get();
    public string SettingsTestConnection => Get();
    public string SettingsSave => Get();
    public string SettingsCancel => Get();
    public string SettingsReset => Get();
    public string SettingsResetConfirm => Get();
    public string TestSuccess => Get();
    public string TestUnauthorized => Get();
    public string TestForbidden => Get();
    public string TestUnreachable => Get();
    public string TestTimeout => Get();
    public string TestTls => Get();
    public string TestInvalidConfiguration => Get();
    public string TestUnexpectedApi => Get();
    public string TestUnavailable => Get();
    public string TestServicesReachable => Get();
    public string TestHostsReachable => Get();
    public string SettingsSaved => Get();
    public string SettingsValidationFailed => Get();
    public string TestNotificationSound => Get();
    public string SoundSection => Get();
    public string SoundDefault => Get();
    public string SoundCustomWav => Get();
    public string SoundChooseWav => Get();
    public string SoundVolume => Get();
    public string SoundRestoreDefault => Get();
    public string SoundMute => Get();
    public string SoundInvalidWav => Get();
    public string SoundWavFilter => Get();
    public string SettingsTabGeneral => Get();
    public string SettingsTabConnection => Get();
    public string SettingsTabNotifications => Get();
    public string SettingsStartWithWindows => Get();
    public string SettingsAutostartFailed => Get();
    public string SettingsNotificationsIntro => Get();
    public string FilterAll => Get();
    public string FilterNew => Get();
    public string FilterCritical => Get();
    public string FilterWarning => Get();
    public string FilterUnknown => Get();
    public string FilterTaken => Get();
    public string EmptyFilterAll => Get();
    public string EmptyFilterNew => Get();
    public string EmptyFilterCritical => Get();
    public string EmptyFilterWarning => Get();
    public string EmptyFilterUnknown => Get();
    public string EmptyFilterTaken => Get();
    public string EmptyFilterSearch => Get();
    public string SearchPlaceholder => Get();
    public string TakenLabel => Get();
    public string MenuConnectionSettings => Get();
    public string MenuHelpAbout => Get();
    public string MenuExit => Get();
    public string MenuOpen => Get();
    public string MenuMuteSound => Get();
    public string MenuUnmuteSound => Get();
    public string MenuHideToTray => Get();
    public string AboutTitle => Get();
    public string AboutDescription => Get();
    public string AboutVersion => Get();
    public string AboutAuthor => Get();
    public string AboutGitHub => Get();
    public string AboutClose => Get();
    public string Take => Get();
    public string Taking => Get();
    public string Taken => Get();
    public string TakenByFormat => Get();
    public string EnableTake => Get();
    public string DisplayName => Get();
    public string TeamCoordination => Get();
    public string TeamCoordinationHint => Get();
    public string TakeConfirmTitle => Get();
    public string TakeConfirmBody => Get();
    public string TakeCouldNot => Get();
    public string TakeForbidden => Get();
    public string TakeAwaitingRefresh => Get();
    public string Release => Get();
    public string Releasing => Get();
    public string ReleaseConfirmTitle => Get();
    public string ReleaseConfirmBody => Get();
    public string ReleaseCouldNot => Get();
    public string ReleaseAwaitingRefresh => Get();

    private string Get([CallerMemberName] string? key = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        return ResourceManager.GetString(key, _culture) ?? key;
    }
}
