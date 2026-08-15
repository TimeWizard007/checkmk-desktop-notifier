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

    private string Get([CallerMemberName] string? key = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        return ResourceManager.GetString(key, _culture) ?? key;
    }
}
