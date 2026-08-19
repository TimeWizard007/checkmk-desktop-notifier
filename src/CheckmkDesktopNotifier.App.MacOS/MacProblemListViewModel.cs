using System.Collections.ObjectModel;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
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
    private readonly ProblemListViewState _listView = new();

    public MacProblemListViewModel(
        IAlertStateService alerts,
        IProblemPoller poller,
        ICheckmkProblemNavigator navigator,
        IUiThread uiThread,
        LoadedConfiguration loaded,
        IMonitoringCoordinator? coordinator = null)
    {
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _uiThread = uiThread ?? throw new ArgumentNullException(nameof(uiThread));
        _loaded = loaded ?? throw new ArgumentNullException(nameof(loaded));
        _coordinator = coordinator;
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

    public bool HasRows => Rows.Count > 0;

    public bool HasEmptyText => !string.IsNullOrWhiteSpace(EmptyText);

    public Action? RequestSettings { get; set; }

    public Action? RequestOpenSite { get; set; }

    public Action? RequestQuit { get; set; }

    public ProblemListFilter ActiveFilter => _listView.ActiveFilter;

    public bool IsFilterAll => ActiveFilter == ProblemListFilter.All;

    public bool IsFilterNew => ActiveFilter == ProblemListFilter.New;

    public bool IsFilterCritical => ActiveFilter == ProblemListFilter.Critical;

    public bool IsFilterWarning => ActiveFilter == ProblemListFilter.Warning;

    public bool IsFilterUnknown => ActiveFilter == ProblemListFilter.Unknown;

    public bool IsFilterTaken => ActiveFilter == ProblemListFilter.Taken;

    public bool IsConfigured => _coordinator?.IsPollingEnabled == true || _loaded.IsUsableReal;

    public MacMenuBarConnectionState ConnectionState { get; private set; } =
        MacMenuBarConnectionState.NotConfigured;

    public event EventHandler? MenuBarChanged;

    public void StartListening()
    {
        _poller.StateChanged += OnPollerStateChanged;
        Reload();
    }

    public void Reload()
    {
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
            Rows.Add(new MacProblemRowViewModel(incident, OpenInCheckmk, ToggleSeen));
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
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasEmptyText));
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

    private void OnPollerStateChanged(object? sender, EventArgs e) =>
        _uiThread.Post(Reload);
}
