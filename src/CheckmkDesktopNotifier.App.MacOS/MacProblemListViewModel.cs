using System.Collections.ObjectModel;
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

namespace CheckmkDesktopNotifier.App.MacOS;

public sealed partial class MacProblemListViewModel : ObservableObject
{
    private readonly IAlertStateService _alerts;
    private readonly IProblemPoller _poller;
    private readonly ICheckmkProblemNavigator _navigator;
    private readonly IUiThread _uiThread;
    private readonly IMonitoringCoordinator? _coordinator;
    private readonly LoadedConfiguration _loaded;
    private readonly ITakeService? _take;
    private readonly IUserPreferences? _preferences;
    private readonly TakeSessionState? _takeSession;
    private readonly ProblemListViewState _listView = new();
    private MonitoredObjectId? _takingId;
    private MonitoredObjectId? _releasingId;

    public MacProblemListViewModel(
        IAlertStateService alerts,
        IProblemPoller poller,
        ICheckmkProblemNavigator navigator,
        IUiThread uiThread,
        LoadedConfiguration loaded,
        IMonitoringCoordinator? coordinator = null,
        ITakeService? take = null,
        IUserPreferences? preferences = null,
        TakeSessionState? takeSession = null)
    {
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _uiThread = uiThread ?? throw new ArgumentNullException(nameof(uiThread));
        _loaded = loaded ?? throw new ArgumentNullException(nameof(loaded));
        _coordinator = coordinator;
        _take = take;
        _preferences = preferences;
        _takeSession = takeSession;
        _listView.OpenFilter(ProblemListFilter.All);
        Reload();
    }

    public ObservableCollection<MacProblemRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _connectionLabel = MacMenuBarStatus.FormatConnectionLabel(MacMenuBarConnectionState.NotConfigured);

    [ObservableProperty]
    private string _menuBarTitle = "Checkmk";

    [ObservableProperty]
    private string _menuBarToolTip = "Checkmk Desktop Notifier";

    [ObservableProperty]
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
    private string _emptyText = "No problems.";

    [ObservableProperty]
    private string _errorText = string.Empty;

    public bool HasRows => Rows.Count > 0;

    public bool HasEmptyText => !string.IsNullOrWhiteSpace(EmptyText);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public Action? RequestSettings { get; set; }

    public Action? RequestOpenSite { get; set; }

    public Action? RequestQuit { get; set; }

    public Func<string, string, string, Task<bool?>>? Confirm { get; set; }

    public ProblemListFilter ActiveFilter => _listView.ActiveFilter;

    public bool IsFilterAll => ActiveFilter == ProblemListFilter.All;

    public bool IsFilterNew => ActiveFilter == ProblemListFilter.New;

    public bool IsFilterCritical => ActiveFilter == ProblemListFilter.Critical;

    public bool IsFilterWarning => ActiveFilter == ProblemListFilter.Warning;

    public bool IsFilterUnknown => ActiveFilter == ProblemListFilter.Unknown;

    public bool IsFilterTaken => ActiveFilter == ProblemListFilter.Taken;

    public string FilterAllLabel => "ALL";

    public string FilterNewLabel => "NEW " + NewCount;

    public string FilterCriticalLabel => "CRIT " + CriticalCount;

    public string FilterWarningLabel => "WARN " + WarningCount;

    public string FilterUnknownLabel => "UNK " + UnknownCount;

    public string FilterTakenLabel => "TAKEN " + TakenCount;

    public bool IsConfigured => _coordinator?.IsPollingEnabled == true || _loaded.IsUsableReal;

    public MacMenuBarConnectionState ConnectionState { get; private set; } =
        MacMenuBarConnectionState.NotConfigured;

    public event EventHandler? MenuBarChanged;

    public void StartListening()
    {
        _poller.StateChanged += OnPollerStateChanged;
        if (_preferences is not null)
        {
            _preferences.Changed += (_, _) => _uiThread.Post(Reload);
        }

        Reload();
    }

    public void FocusProblem(MonitoredObjectId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        SearchText = id.Kind == ObjectKind.Host
            ? id.HostName
            : id.HostName + " " + (id.ServiceDescription ?? string.Empty);
        _listView.OpenFilter(ProblemListFilter.All);
        Reload();
    }

    public void Reload()
    {
        ClearInFlightIfConverged();
        var incidents = _alerts.GetOpenIncidents();
        var newestFirst = incidents
            .OrderByDescending(incident => incident.OpenedAtUtc)
            .ThenByDescending(incident => incident.LastObservedAtUtc)
            .ToArray();

        var counts = MacMenuBarStatus.FromIncidents(newestFirst);
        NewCount = counts.New;
        CriticalCount = counts.Critical;
        WarningCount = counts.Warning;
        UnknownCount = counts.Unknown;
        TakenCount = counts.Taken;

        ConnectionState = MacMenuBarStatus.FromSession(IsConfigured, _poller.Status);
        ConnectionLabel = MacMenuBarStatus.FormatConnectionLabel(ConnectionState);
        MenuBarTitle = MacMenuBarStatus.FormatTitle(counts, ConnectionState);
        MenuBarToolTip = MacMenuBarStatus.FormatToolTip(counts, ConnectionState);

        var filtered = ProblemListFilterLogic.Apply(newestFirst, ActiveFilter, SearchText);
        Rows.Clear();
        foreach (var incident in filtered)
        {
            Rows.Add(ToRow(incident));
        }

        EmptyText = Rows.Count == 0
            ? ConnectionState switch
            {
                MacMenuBarConnectionState.NotConfigured => "Configure Checkmk in Settings.",
                MacMenuBarConnectionState.Error => "Connection error. Check VPN and Settings.",
                MacMenuBarConnectionState.Disconnected => "Waiting for Checkmk…",
                _ => "No problems match this filter."
            }
            : string.Empty;

        OnPropertyChanged(nameof(ActiveFilter));
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterNew));
        OnPropertyChanged(nameof(IsFilterCritical));
        OnPropertyChanged(nameof(IsFilterWarning));
        OnPropertyChanged(nameof(IsFilterUnknown));
        OnPropertyChanged(nameof(IsFilterTaken));
        OnPropertyChanged(nameof(FilterAllLabel));
        OnPropertyChanged(nameof(FilterNewLabel));
        OnPropertyChanged(nameof(FilterCriticalLabel));
        OnPropertyChanged(nameof(FilterWarningLabel));
        OnPropertyChanged(nameof(FilterUnknownLabel));
        OnPropertyChanged(nameof(FilterTakenLabel));
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasEmptyText));
        OnPropertyChanged(nameof(HasError));
        MenuBarChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectAllFilter() => SelectFilter(ProblemListFilter.All);

    [RelayCommand]
    private void SelectNewFilter() => SelectFilter(ProblemListFilter.New);

    [RelayCommand]
    private void SelectCriticalFilter() => SelectFilter(ProblemListFilter.Critical);

    [RelayCommand]
    private void SelectWarningFilter() => SelectFilter(ProblemListFilter.Warning);

    [RelayCommand]
    private void SelectUnknownFilter() => SelectFilter(ProblemListFilter.Unknown);

    [RelayCommand]
    private void SelectTakenFilter() => SelectFilter(ProblemListFilter.Taken);

    [RelayCommand]
    private void OpenSettings() => RequestSettings?.Invoke();

    [RelayCommand]
    private void OpenSite() => RequestOpenSite?.Invoke();

    [RelayCommand]
    private void Quit() => RequestQuit?.Invoke();

    partial void OnSearchTextChanged(string value) => Reload();

    private void SelectFilter(ProblemListFilter filter)
    {
        _listView.OpenFilter(filter);
        Reload();
    }

    private MacProblemRowViewModel ToRow(OpenIncident incident)
    {
        var isTakingThis = _takingId is not null && _takingId.Equals(incident.ObjectId);
        var isReleasingThis = _releasingId is not null && _releasingId.Equals(incident.ObjectId);
        var isBusy = _takingId is not null || _releasingId is not null;
        var real = _loaded.IsUsableReal && _take is not null && _loaded.IsMock != true;
        var canOffer = TakeEligibility.CanOfferTake(
            _preferences?.TakeEnabled == true,
            _preferences?.TakeDisplayName,
            isRealMonitoring: real,
            acknowledgeForbidden: _takeSession?.AcknowledgeForbidden == true,
            alreadyAcknowledged: incident.IsAcknowledgedInCheckmk,
            isTaking: isBusy,
            isReady: true);
        var canOfferRelease = TakeEligibility.CanOfferRelease(
            incident.IsAcknowledgedInCheckmk,
            incident.IsTakenByNotifier,
            isRealMonitoring: real,
            acknowledgeForbidden: _takeSession?.AcknowledgeForbidden == true,
            isBusy: isBusy,
            isReady: true);
        var visual = TakeRowPresentation.Classify(
            incident.IsAcknowledgedInCheckmk,
            incident.IsTakenByNotifier,
            canOffer,
            isTakingThis,
            isReleasingThis);
        return new MacProblemRowViewModel(
            incident,
            OpenInCheckmk,
            ToggleSeen,
            visual,
            canOfferRelease && !isReleasingThis,
            TakeProblem,
            ReleaseProblem);
    }

    private void OpenInCheckmk(MacProblemRowViewModel row)
    {
        try
        {
            _navigator.Open(row.ObjectId);
        }
        catch (Exception)
        {
        }
    }

    private void ToggleSeen(MacProblemRowViewModel row)
    {
        if (row.IsNew)
        {
            _alerts.MarkSeen(row.ObjectId);
        }
        else
        {
            _alerts.MarkUnseen(row.ObjectId);
        }

        Reload();
    }

    private async void TakeProblem(MacProblemRowViewModel row)
    {
        var take = _take;
        if (take is null || !row.CanTake || _takingId is not null || _releasingId is not null)
        {
            return;
        }

        try
        {
            var answered = Confirm is null
                ? false
                : await Confirm(MacUiCopy.TakeTitle, MacUiCopy.TakeBody, MacUiCopy.Take).ConfigureAwait(true);
            if (!TakeConfirmation.ShouldProceed(answered))
            {
                return;
            }
        }
        catch (Exception)
        {
            return;
        }

        _takingId = row.ObjectId;
        ErrorText = string.Empty;
        Reload();
        TakeOperationResult result;
        try
        {
            result = await take.TakeAsync(row.ObjectId).ConfigureAwait(true);
        }
        catch (Exception)
        {
            result = TakeOperationResult.Unavailable;
        }

        FinishWrite(ref _takingId, result, MacUiCopy.TakeCouldNot);
    }

    private async void ReleaseProblem(MacProblemRowViewModel row)
    {
        var take = _take;
        if (take is null || !row.CanRelease || _takingId is not null || _releasingId is not null)
        {
            return;
        }

        try
        {
            var body = string.Format(MacUiCopy.ReleaseBodyFormat, row.TakenByDisplayName);
            var answered = Confirm is null
                ? false
                : await Confirm(MacUiCopy.ReleaseTitle, body, MacUiCopy.Release).ConfigureAwait(true);
            if (!TakeConfirmation.ShouldProceed(answered))
            {
                return;
            }
        }
        catch (Exception)
        {
            return;
        }

        _releasingId = row.ObjectId;
        ErrorText = string.Empty;
        Reload();
        TakeOperationResult result;
        try
        {
            result = await take.ReleaseAsync(row.ObjectId).ConfigureAwait(true);
        }
        catch (Exception)
        {
            result = TakeOperationResult.Unavailable;
        }

        FinishWrite(ref _releasingId, result, MacUiCopy.ReleaseCouldNot);
    }

    private void FinishWrite(ref MonitoredObjectId? inFlightId, TakeOperationResult result, string fallbackError)
    {
        if (!TakeCompletionUi.KeepWaitingVisual(result.Status))
        {
            inFlightId = null;
        }

        Reload();
        if (!TakeCompletionUi.ShowsErrorDialog(result.Status))
        {
            return;
        }

        ErrorText = result.Status == TakeOperationStatus.Forbidden ? MacUiCopy.TakeForbidden : fallbackError;
        OnPropertyChanged(nameof(HasError));
    }

    private void ClearInFlightIfConverged()
    {
        var incidents = _alerts.GetOpenIncidents();
        if (_takingId is { } taking)
        {
            var incident = incidents.FirstOrDefault(open => open.ObjectId.Equals(taking));
            if (incident is null || incident.IsAcknowledgedInCheckmk)
            {
                _takingId = null;
            }
        }

        if (_releasingId is { } releasing)
        {
            var incident = incidents.FirstOrDefault(open => open.ObjectId.Equals(releasing));
            if (incident is null || !incident.IsTakenByNotifier)
            {
                _releasingId = null;
            }
        }
    }

    private void OnPollerStateChanged(object? sender, EventArgs e) =>
        _uiThread.Post(Reload);
}
