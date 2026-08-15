using System.Globalization;
using System.Resources;

namespace CheckmkDesktopNotifier.App.Localization;

public interface ILocalizationService
{
    CultureInfo Culture { get; }

    void SetCulture(CultureInfo culture);

    string CompactBarTitle { get; }
    string NewLabel { get; }
    string CriticalLabel { get; }
    string WarningLabel { get; }
    string UnknownLabel { get; }
    string LastCheckLabel { get; }
    string LastCheckUnknown { get; }
    string MarkAllNewAsSeen { get; }
    string MarkAsSeen { get; }
    string Seen { get; }
    string Acknowledged { get; }
    string AcknowledgedTooltip { get; }
    string Downtime { get; }
    string DowntimeTooltip { get; }
    string HostKind { get; }
    string NewSection { get; }
    string CriticalSection { get; }
    string WarningSection { get; }
    string UnknownSection { get; }
    string NoNewProblems { get; }
    string NoProblems { get; }
    string ProblemListTitle { get; }
    string SeverityCritical { get; }
    string SeverityWarning { get; }
    string SeverityUnknown { get; }
}
