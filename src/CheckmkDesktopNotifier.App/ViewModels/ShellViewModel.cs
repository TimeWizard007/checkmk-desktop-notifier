using System.Collections.ObjectModel;
using System.Windows;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IAlertStateService _alerts;
    private readonly IProblemPoller _poller;
    private readonly TimeProvider _clock;

    public ShellViewModel(
        IAlertStateService alerts,
        ILocalizationService text,
        TimeProvider clock,
        IProblemPoller poller)
    {
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _poller.StateChanged += OnPollerStateChanged;
        Reload();
    }

    public ILocalizationService Text { get; }

    public ObservableCollection<ProblemItemViewModel> NewItems { get; } = [];

    public ObservableCollection<ProblemItemViewModel> CriticalItems { get; } = [];

    public ObservableCollection<ProblemItemViewModel> WarningItems { get; } = [];

    public ObservableCollection<ProblemItemViewModel> UnknownItems { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewProblems))]
    [NotifyCanExecuteChangedFor(nameof(MarkAllNewAsSeenCommand))]
    private int _newCount;

    [ObservableProperty]
    private int _criticalCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _unknownCount;

    [ObservableProperty]
    private string _lastCheckText = string.Empty;

    [ObservableProperty]
    private string _connectionStatusText = string.Empty;

    public bool HasNewProblems => NewCount > 0;

    public void Reload()
    {
        var incidents = _alerts.GetOpenIncidents();
        var newestFirst = incidents
            .OrderByDescending(incident => incident.OpenedAtUtc)
            .ThenByDescending(incident => incident.LastObservedAtUtc)
            .ToArray();

        Replace(NewItems, newestFirst.Where(incident => !incident.IsSeen).Select(ToItem));
        Replace(CriticalItems, newestFirst.Where(incident => incident.Severity == Severity.Critical).Select(ToItem));
        Replace(WarningItems, newestFirst.Where(incident => incident.Severity == Severity.Warning).Select(ToItem));
        Replace(UnknownItems, newestFirst.Where(incident => incident.Severity == Severity.Unknown).Select(ToItem));

        NewCount = NewItems.Count;
        CriticalCount = CriticalItems.Count;
        WarningCount = WarningItems.Count;
        UnknownCount = UnknownItems.Count;
        LastCheckText = FormatLastCheck(_alerts.LastSuccessfulPollUtc);
        ConnectionStatusText = FormatConnectionStatus(_poller.Status);
        OnPropertyChanged(nameof(HasNewProblems));
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand(CanExecute = nameof(CanMarkAllNewAsSeen))]
    private void MarkAllNewAsSeen()
    {
        _alerts.MarkAllNewAsSeen();
        Reload();
    }

    private bool CanMarkAllNewAsSeen() => NewCount > 0;

    [RelayCommand]
    private void MarkSeen(ProblemItemViewModel? item)
    {
        if (item is null || !item.IsNew)
        {
            return;
        }

        _alerts.MarkSeen(item.ObjectId);
        Reload();
    }

    private void OnPollerStateChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Reload();
            return;
        }

        dispatcher.BeginInvoke(Reload);
    }

    private ProblemItemViewModel ToItem(OpenIncident incident) =>
        new()
        {
            ObjectId = incident.ObjectId,
            HostName = incident.ObjectId.HostName,
            ServiceName = incident.ObjectId.Kind == ObjectKind.Host
                ? Text.HostKind
                : incident.ObjectId.ServiceDescription ?? string.Empty,
            SeverityText = incident.Severity switch
            {
                Severity.Critical => Text.SeverityCritical,
                Severity.Warning => Text.SeverityWarning,
                Severity.Unknown => Text.SeverityUnknown,
                _ => incident.Severity.ToString()
            },
            Severity = incident.Severity,
            PluginOutput = incident.LastSummary,
            IsNew = !incident.IsSeen,
            IsSeen = incident.IsSeen,
            IsAcknowledged = incident.IsAcknowledgedInCheckmk,
            IsInDowntime = incident.ScheduledDowntimeDepth > 0
        };

    private string FormatLastCheck(DateTimeOffset? retrievedAt)
    {
        if (retrievedAt is null)
        {
            return Text.LastCheckUnknown;
        }

        var local = TimeZoneInfo.ConvertTime(retrievedAt.Value, _clock.LocalTimeZone);
        return local.ToString("HH:mm");
    }

    private string FormatConnectionStatus(ConnectionStatus status) =>
        status.Kind switch
        {
            ConnectionStatusKind.Refreshing => Text.ConnectionRefreshing,
            ConnectionStatusKind.Error => Text.ConnectionError,
            ConnectionStatusKind.Connected => Text.ConnectionConnected,
            _ when _alerts.LastSuccessfulPollUtc is not null => Text.ConnectionConnected,
            _ => string.Empty
        };

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
