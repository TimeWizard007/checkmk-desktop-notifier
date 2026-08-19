using System.Collections.ObjectModel;
using System.ComponentModel;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Threading;
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
    private readonly ITakeService? _take;
    private readonly ICheckmkProblemNavigator? _navigator;
    private readonly TakeSessionState? _takeSession;
    private readonly LoadedConfiguration? _loaded;
    private readonly IUiThread _uiThread;
    private readonly ProblemListViewState _listView = new();
    private readonly bool _settingsAvailable;
    private ShellPhase _phase = ShellPhase.Initializing;
    private MonitoredObjectId? _takingId;
    private MonitoredObjectId? _releasingId;

    public ShellViewModel(
        IAlertStateService alerts,
        ILocalizationService text,
        TimeProvider clock,
        IProblemPoller poller,
        IMonitoringCoordinator? coordinator = null,
        LoadedConfiguration? loaded = null,
        Lazy<IShellCommands>? shell = null,
        IUserPreferences? preferences = null,
        ITakeService? take = null,
        TakeSessionState? takeSession = null,
        ICheckmkProblemNavigator? navigator = null,
        IUiThread? uiThread = null)
    {
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _coordinator = coordinator;
        _shell = shell;
        _preferences = preferences ?? new InMemoryUserPreferences();
        _take = take;
        _navigator = navigator;
        _takeSession = takeSession;
        _loaded = loaded;
        _uiThread = uiThread ?? ImmediateUiThread.Instance;
        _settingsAvailable = loaded?.IsMock != true;
        _poller.StateChanged += OnPollerStateChanged;
        _preferences.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(MuteMenuHeader));
            TakeCommand.NotifyCanExecuteChanged();
            ReleaseCommand.NotifyCanExecuteChanged();
            Reload();
        };
        if (_takeSession is not null)
        {
            _takeSession.Changed += (_, _) =>
            {
                TakeCommand.NotifyCanExecuteChanged();
                ReleaseCommand.NotifyCanExecuteChanged();
                Reload();
            };
        }
        if (Text is INotifyPropertyChanged textChanged)
        {
            textChanged.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(MuteMenuHeader));
                OnPropertyChanged(nameof(EmptyFilterText));
                OnPropertyChanged(nameof(HasSearchQuery));
                OnPropertyChanged(nameof(ShowSearchPlaceholder));
            };
        }

        Reload();
    }

    public ILocalizationService Text { get; }

    public Func<string, string, bool>? ConfirmTake { get; set; }

    public Func<string, string, bool>? ConfirmRelease { get; set; }

    public Action<string>? ShowTakeMessage { get; set; }

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
    private int _takenCount;

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchQuery));
        OnPropertyChanged(nameof(ShowSearchPlaceholder));
        Reload();
    }

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

    public bool IsFilterTaken => ActiveFilter == ProblemListFilter.Taken;

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchText);

    public bool ShowSearchPlaceholder => string.IsNullOrEmpty(SearchText);

    public bool ShowSectionedList =>
        IsFilterAll && !HasSearchQuery && (NewCount + CriticalCount + WarningCount + UnknownCount) > 0;

    public bool ShowFilteredList => (!IsFilterAll || HasSearchQuery) && FilteredItems.Count > 0;

    public bool ShowEmptyFilter => !ShowSectionedList && !ShowFilteredList;

    public string EmptyFilterText
    {
        get
        {
            if (HasSearchQuery)
            {
                return Text.EmptyFilterSearch;
            }

            return ActiveFilter switch
            {
                ProblemListFilter.New => Text.EmptyFilterNew,
                ProblemListFilter.Critical => Text.EmptyFilterCritical,
                ProblemListFilter.Warning => Text.EmptyFilterWarning,
                ProblemListFilter.Unknown => Text.EmptyFilterUnknown,
                ProblemListFilter.Taken => Text.EmptyFilterTaken,
                _ => Text.EmptyFilterAll
            };
        }
    }

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
        ToggleTakenFilterCommand.NotifyCanExecuteChanged();
        SelectAllFilterCommand.NotifyCanExecuteChanged();
        SelectNewFilterCommand.NotifyCanExecuteChanged();
        SelectCriticalFilterCommand.NotifyCanExecuteChanged();
        SelectWarningFilterCommand.NotifyCanExecuteChanged();
        SelectUnknownFilterCommand.NotifyCanExecuteChanged();
        SelectTakenFilterCommand.NotifyCanExecuteChanged();
        TakeCommand.NotifyCanExecuteChanged();
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
        ToggleTakenFilterCommand.NotifyCanExecuteChanged();
        SelectAllFilterCommand.NotifyCanExecuteChanged();
        SelectNewFilterCommand.NotifyCanExecuteChanged();
        SelectCriticalFilterCommand.NotifyCanExecuteChanged();
        SelectWarningFilterCommand.NotifyCanExecuteChanged();
        SelectUnknownFilterCommand.NotifyCanExecuteChanged();
        SelectTakenFilterCommand.NotifyCanExecuteChanged();
        TakeCommand.NotifyCanExecuteChanged();
        Reload();
    }

    public void Reload()
    {
        if (ClearInFlightIfConverged())
        {
            TakeCommand.NotifyCanExecuteChanged();
            ReleaseCommand.NotifyCanExecuteChanged();
        }

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
            ProblemListFilterLogic.Apply(newestFirst, ActiveFilter, SearchText).Select(ToItem));

        NewCount = NewItems.Count;
        CriticalCount = CriticalItems.Count;
        WarningCount = WarningItems.Count;
        UnknownCount = UnknownItems.Count;
        TakenCount = ProblemListFilterLogic.CountTaken(newestFirst);
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
        OnPropertyChanged(nameof(IsFilterTaken));
        OnPropertyChanged(nameof(HasSearchQuery));
        OnPropertyChanged(nameof(ShowSearchPlaceholder));
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
    private void ToggleTakenFilter() => ToggleCounter(ProblemListFilter.Taken);

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

    [RelayCommand(CanExecute = nameof(CanToggleExpanded))]
    private void SelectTakenFilter() => SelectFilter(ProblemListFilter.Taken);

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
    private void ToggleSeen(ProblemItemViewModel? item)
    {
        if (item is null || _phase != ShellPhase.Ready)
        {
            return;
        }

        if (item.IsNew)
        {
            _alerts.MarkSeen(item.ObjectId);
        }
        else
        {
            _alerts.MarkUnseen(item.ObjectId);
        }

        Reload();
    }

    [RelayCommand]
    private void OpenInCheckmk(ProblemItemViewModel? item)
    {
        if (item is null || _phase != ShellPhase.Ready || _navigator is null)
        {
            return;
        }

        try
        {
            _navigator.Open(item.ObjectId);
        }
        catch (Exception)
        {
        }
    }

    [RelayCommand(CanExecute = nameof(CanTake))]
    private async Task TakeAsync(ProblemItemViewModel? item)
    {
        if (item is null || _take is null || _takingId is not null || _releasingId is not null || _phase != ShellPhase.Ready)
        {
            return;
        }

        if (!item.CanTake)
        {
            return;
        }

        try
        {
            if (!TakeConfirmation.ShouldProceed(
                    ConfirmTake?.Invoke(Text.TakeConfirmTitle, Text.TakeConfirmBody)))
            {
                return;
            }
        }
        catch (Exception)
        {
            return;
        }

        _takingId = item.ObjectId;
        TakeCommand.NotifyCanExecuteChanged();
        ReleaseCommand.NotifyCanExecuteChanged();
        Reload();
        TakeOperationResult result;
        try
        {
            result = await _take.TakeAsync(item.ObjectId).ConfigureAwait(true);
        }
        catch (Exception)
        {
            result = TakeOperationResult.Unavailable;
        }

        FinishWrite(ref _takingId, result, Text.TakeCouldNot);
    }

    private bool CanTake(ProblemItemViewModel? item) =>
        item is not null
        && item.CanTake
        && _takingId is null
        && _releasingId is null
        && _phase == ShellPhase.Ready
        && _take is not null;

    [RelayCommand(CanExecute = nameof(CanRelease))]
    private async Task ReleaseAsync(ProblemItemViewModel? item)
    {
        if (item is null || _take is null || _takingId is not null || _releasingId is not null || _phase != ShellPhase.Ready)
        {
            return;
        }

        if (!item.CanRelease)
        {
            return;
        }

        try
        {
            var body = string.Format(Text.ReleaseConfirmBody, item.TakenByDisplayName ?? string.Empty);
            if (!TakeConfirmation.ShouldProceed(
                    ConfirmRelease?.Invoke(Text.ReleaseConfirmTitle, body)))
            {
                return;
            }
        }
        catch (Exception)
        {
            return;
        }

        _releasingId = item.ObjectId;
        TakeCommand.NotifyCanExecuteChanged();
        ReleaseCommand.NotifyCanExecuteChanged();
        Reload();
        TakeOperationResult result;
        try
        {
            result = await _take.ReleaseAsync(item.ObjectId).ConfigureAwait(true);
        }
        catch (Exception)
        {
            result = TakeOperationResult.Unavailable;
        }

        FinishWrite(ref _releasingId, result, Text.ReleaseCouldNot);
    }

    private bool CanRelease(ProblemItemViewModel? item) =>
        item is not null
        && item.CanRelease
        && _takingId is null
        && _releasingId is null
        && _phase == ShellPhase.Ready
        && _take is not null;

    private void FinishWrite(ref MonitoredObjectId? inFlightId, TakeOperationResult result, string fallbackError)
    {
        if (!TakeCompletionUi.KeepWaitingVisual(result.Status))
        {
            inFlightId = null;
        }

        TakeCommand.NotifyCanExecuteChanged();
        ReleaseCommand.NotifyCanExecuteChanged();
        Reload();

        if (!TakeCompletionUi.ShowsErrorDialog(result.Status))
        {
            return;
        }

        var message = result.Status == TakeOperationStatus.Forbidden ? Text.TakeForbidden : fallbackError;
        try
        {
            ShowTakeMessage?.Invoke(message);
        }
        catch (Exception)
        {
        }
    }

    private bool ClearInFlightIfConverged()
    {
        var changed = false;
        var incidents = _alerts.GetOpenIncidents();
        if (_takingId is { } taking)
        {
            var incident = incidents.FirstOrDefault(open => open.ObjectId.Equals(taking));
            if (incident is null || incident.IsAcknowledgedInCheckmk)
            {
                _takingId = null;
                changed = true;
            }
        }

        if (_releasingId is { } releasing)
        {
            var incident = incidents.FirstOrDefault(open => open.ObjectId.Equals(releasing));
            if (incident is null || !incident.IsTakenByNotifier)
            {
                _releasingId = null;
                changed = true;
            }
        }

        return changed;
    }

    private void OnPollerStateChanged(object? sender, EventArgs e)
    {
        if (_uiThread.CheckAccess())
        {
            Reload();
            return;
        }

        _uiThread.Post(Reload);
    }

    private ProblemItemViewModel ToItem(OpenIncident incident)
    {
        var isTakingThis = _takingId is not null && _takingId.Equals(incident.ObjectId);
        var isReleasingThis = _releasingId is not null && _releasingId.Equals(incident.ObjectId);
        var isBusy = _takingId is not null || _releasingId is not null;
        var canOffer = TakeEligibility.CanOfferTake(
            _preferences.TakeEnabled,
            _preferences.TakeDisplayName,
            isRealMonitoring: _loaded?.IsMock != true && _take is not null,
            acknowledgeForbidden: _takeSession?.AcknowledgeForbidden == true,
            alreadyAcknowledged: incident.IsAcknowledgedInCheckmk,
            isTaking: isBusy,
            isReady: _phase == ShellPhase.Ready);
        var canOfferRelease = TakeEligibility.CanOfferRelease(
            incident.IsAcknowledgedInCheckmk,
            incident.IsTakenByNotifier,
            isRealMonitoring: _loaded?.IsMock != true && _take is not null,
            acknowledgeForbidden: _takeSession?.AcknowledgeForbidden == true,
            isBusy: isBusy,
            isReady: _phase == ShellPhase.Ready);
        var visual = TakeRowPresentation.Classify(
            incident.IsAcknowledgedInCheckmk,
            incident.IsTakenByNotifier,
            canOffer,
            isTakingThis,
            isReleasingThis);
        var acknowledgementLabel = visual switch
        {
            TakeRowVisual.Taken => string.Format(
                Text.TakenByFormat,
                incident.TakenByDisplayName ?? string.Empty),
            TakeRowVisual.Releasing => Text.Releasing,
            TakeRowVisual.Acknowledged => Text.Acknowledged,
            _ => string.Empty
        };

        return new ProblemItemViewModel
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
            IsInDowntime = incident.ScheduledDowntimeDepth > 0,
            TakeVisual = visual,
            AcknowledgementLabel = acknowledgementLabel,
            TakenByDisplayName = incident.TakenByDisplayName,
            CanRelease = canOfferRelease && !isReleasingThis,
            TakeButtonText = visual == TakeRowVisual.Taking ? Text.Taking : Text.Take,
            EyeTooltip = incident.IsSeen ? Text.MarkAsUnseen : Text.MarkAsSeen
        };
    }

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
