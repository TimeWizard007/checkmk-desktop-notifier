using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CheckmkDesktopNotifier.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IAlertStateService _alerts;
    private readonly IProblemPoller _poller;
    private readonly TimeProvider _clock;
    private readonly IMonitoringCoordinator? _coordinator;
    private readonly Lazy<IShellCommands>? _shell;
    private readonly IUserPreferences _preferences;
    private readonly ProblemListViewState _listView = new();
    private readonly bool _settingsAvailable;
    private ShellPhase _phase = ShellPhase.Initializing;

    public ShellViewModel(
        IAlertStateService alerts,
        ILocalizationService text,
        TimeProvider clock,
        IProblemPoller poller,
        IMonitoringCoordinator? coordinator = null,
        LoadedConfiguration? loaded = null,
        Lazy<IShellCommands>? shell = null,
        IUserPreferences? preferences = null)
    {
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _coordinator = coordinator;
        _shell = shell;
        _preferences = preferences ?? new InMemoryUserPreferences();
        _settingsAvailable = loaded?.IsMock != true;
        _poller.StateChanged += OnPollerStateChanged;
        _preferences.Changed += (_, _) => OnPropertyChanged(nameof(MuteMenuHeader));
        if (Text is INotifyPropertyChanged textChanged)
        {
            textChanged.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(MuteMenuHeader));
                OnPropertyChanged(nameof(EmptyFilterText));
            };
        }

        Reload();
    }

    public ILocalizationService Text { get; }

    public ObservableCollection<ProblemItemViewModel> NewItems { get; } = [];

    public ObservableCollection<ProblemItemViewModel> CriticalItems { get; } = [];

    public ObservableCollection<ProblemItemViewModel> WarningItems { get; } = [];

    public ObservableCollection<ProblemItemViewModel> UnknownItems { get; } = [];

    public ObservableCollection<ProblemItemViewModel> FilteredItems { get; } = [];

    public bool SettingsAvailable => _settingsAvailable;

    public bool IsReady => _phase == ShellPhase.Ready;

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value)
        {
            _listView.Collapse();
        }
    }

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

    public ProblemListFilter ActiveFilter => _listView.ActiveFilter;

    public bool IsFilterAll => ActiveFilter == ProblemListFilter.All;

    public bool IsFilterNew => ActiveFilter == ProblemListFilter.New;

    public bool IsFilterCritical => ActiveFilter == ProblemListFilter.Critical;

    public bool IsFilterWarning => ActiveFilter == ProblemListFilter.Warning;

    public bool IsFilterUnknown => ActiveFilter == ProblemListFilter.Unknown;

    public bool ShowSectionedList => IsFilterAll && (NewCount + CriticalCount + WarningCount + UnknownCount) > 0;

    public bool ShowFilteredList => !IsFilterAll && FilteredItems.Count > 0;

    public bool ShowEmptyFilter => !ShowSectionedList && !ShowFilteredList;

    public string EmptyFilterText => ActiveFilter switch
    {
        ProblemListFilter.New => Text.EmptyFilterNew,
        ProblemListFilter.Critical => Text.EmptyFilterCritical,
        ProblemListFilter.Warning => Text.EmptyFilterWarning,
        ProblemListFilter.Unknown => Text.EmptyFilterUnknown,
        _ => Text.EmptyFilterAll
    };

    public string MuteMenuHeader =>
        MuteCommands.MenuHeader(_preferences, Text.MenuMuteSound, Text.MenuUnmuteSound);

    public void CompleteInitialization()
    {
        if (_phase == ShellPhase.ShuttingDown)
        {
            return;
        }

        _phase = ShellPhase.Ready;
        OpenSettingsCommand.NotifyCanExecuteChanged();
        ToggleExpandedCommand.NotifyCanExecuteChanged();
        ToggleNewFilterCommand.NotifyCanExecuteChanged();
        ToggleCriticalFilterCommand.NotifyCanExecuteChanged();
        ToggleWarningFilterCommand.NotifyCanExecuteChanged();
        ToggleUnknownFilterCommand.NotifyCanExecuteChanged();
        SelectAllFilterCommand.NotifyCanExecuteChanged();
        SelectNewFilterCommand.NotifyCanExecuteChanged();
        SelectCriticalFilterCommand.NotifyCanExecuteChanged();
        SelectWarningFilterCommand.NotifyCanExecuteChanged();
        SelectUnknownFilterCommand.NotifyCanExecuteChanged();
        Reload();
    }

    public void BeginShutdown()
    {
        _phase = ShellPhase.ShuttingDown;
        OpenSettingsCommand.NotifyCanExecuteChanged();
        ToggleExpandedCommand.NotifyCanExecuteChanged();
        ToggleNewFilterCommand.NotifyCanExecuteChanged();
        ToggleCriticalFilterCommand.NotifyCanExecuteChanged();
        ToggleWarningFilterCommand.NotifyCanExecuteChanged();
        ToggleUnknownFilterCommand.NotifyCanExecuteChanged();
        SelectAllFilterCommand.NotifyCanExecuteChanged();
        SelectNewFilterCommand.NotifyCanExecuteChanged();
        SelectCriticalFilterCommand.NotifyCanExecuteChanged();
        SelectWarningFilterCommand.NotifyCanExecuteChanged();
        SelectUnknownFilterCommand.NotifyCanExecuteChanged();
        Reload();
    }

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
        Replace(
            FilteredItems,
            ProblemListFilterLogic.Apply(newestFirst, ActiveFilter).Select(ToItem));

        NewCount = NewItems.Count;
        CriticalCount = CriticalItems.Count;
        WarningCount = WarningItems.Count;
        UnknownCount = UnknownItems.Count;
        LastCheckText = FormatLastCheck(_alerts.LastSuccessfulPollUtc);
        ConnectionStatusText = FormatConnectionStatus(_poller.Status);
        OnPropertyChanged(nameof(HasNewProblems));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(ActiveFilter));
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterNew));
        OnPropertyChanged(nameof(IsFilterCritical));
        OnPropertyChanged(nameof(IsFilterWarning));
        OnPropertyChanged(nameof(IsFilterUnknown));
        OnPropertyChanged(nameof(ShowSectionedList));
        OnPropertyChanged(nameof(ShowFilteredList));
        OnPropertyChanged(nameof(ShowEmptyFilter));
        OnPropertyChanged(nameof(EmptyFilterText));
    }

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void ToggleExpanded()
    {
        _listView.ToggleFromBarBackground();
        IsExpanded = _listView.IsExpanded;
        Reload();
    }

    private bool CanToggleExpanded() => _phase == ShellPhase.Ready;

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void ToggleNewFilter() => ToggleCounter(ProblemListFilter.New);

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void ToggleCriticalFilter() => ToggleCounter(ProblemListFilter.Critical);

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void ToggleWarningFilter() => ToggleCounter(ProblemListFilter.Warning);

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void ToggleUnknownFilter() => ToggleCounter(ProblemListFilter.Unknown);

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void SelectAllFilter() => SelectFilter(ProblemListFilter.All);

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void SelectNewFilter() => SelectFilter(ProblemListFilter.New);

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void SelectCriticalFilter() => SelectFilter(ProblemListFilter.Critical);

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void SelectWarningFilter() => SelectFilter(ProblemListFilter.Warning);

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void SelectUnknownFilter() => SelectFilter(ProblemListFilter.Unknown);

    private void ToggleCounter(ProblemListFilter filter)
    {
        _listView.ToggleCounter(filter);
        IsExpanded = _listView.IsExpanded;
        Reload();
    }

    private void SelectFilter(ProblemListFilter filter)
    {
        _listView.OpenFilter(filter);
        IsExpanded = true;
        Reload();
    }

    [RelayCommand(CanExecute = nameof(CanOpenSettings))]
    private void OpenSettings() => _shell?.Value.ShowSettings();

    // Gear / Settings / About / mute / hide must not call OpenFilter or ToggleFromBarBackground.

    private bool CanOpenSettings() => _settingsAvailable && _phase == ShellPhase.Ready;

    [RelayCommand]
    private void OpenAbout() => _shell?.Value.ShowAbout();

    [RelayCommand]
    private void ExitApplication() => _shell?.Value.Exit();

    [RelayCommand]
    private void ShowBar() => _shell?.Value.ShowBar();

    [RelayCommand]
    private void HideToTray() => _shell?.Value.HideToTray();

    [RelayCommand]
    private void ToggleMute() => MuteCommands.Toggle(_preferences);

    [RelayCommand(CanExecute = nameof(CanMarkAllNewAsSeen))]
    private void MarkAllNewAsSeen()
    {
        _alerts.MarkAllNewAsSeen();
        Reload();
    }

    private bool CanMarkAllNewAsSeen() => NewCount > 0 && _phase == ShellPhase.Ready;

    [RelayCommand]
    private void MarkSeen(ProblemItemViewModel? item)
    {
        if (item is null || !item.IsNew || _phase != ShellPhase.Ready)
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

    private string FormatConnectionStatus(ConnectionStatus status)
    {
        var unconfigured = _coordinator is not null && !_coordinator.IsPollingEnabled;
        var poller = status.Kind switch
        {
            ConnectionStatusKind.Refreshing => SessionPollerKind.Refreshing,
            ConnectionStatusKind.Connected => SessionPollerKind.Connected,
            ConnectionStatusKind.Error => SessionPollerKind.Error,
            _ => SessionPollerKind.Idle
        };

        return ShellConnectionLabelMapper.Map(_phase, unconfigured, poller) switch
        {
            ShellConnectionLabel.Initializing => Text.ConnectionInitializing,
            ShellConnectionLabel.SetupRequired => Text.ConnectionSetupRequired,
            ShellConnectionLabel.Refreshing => Text.ConnectionRefreshing,
            ShellConnectionLabel.Connected => Text.ConnectionConnected,
            ShellConnectionLabel.Error => Text.ConnectionError,
            _ => string.Empty
        };
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
