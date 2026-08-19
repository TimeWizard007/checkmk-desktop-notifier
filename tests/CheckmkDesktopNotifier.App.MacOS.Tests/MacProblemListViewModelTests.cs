using CheckmkDesktopNotifier.App.MacOS;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Mock;
using CheckmkDesktopNotifier.Core.Navigation;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Polling;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class MacProblemListViewModelTests
{
    [Fact]
    public void Filter_and_search_use_shared_problem_list_logic()
    {
        var vm = CreateLoaded();
        Assert.Equal(ProblemListFilter.All, vm.ActiveFilter);
        Assert.True(vm.Rows.Count > 0);

        vm.SelectCriticalFilterCommand.Execute(null);
        Assert.True(vm.IsFilterCritical);
        Assert.All(vm.Rows, row => Assert.Equal(Severity.Critical, row.Severity));

        vm.SelectAllFilterCommand.Execute(null);
        vm.SearchText = "SRV-SQL01";
        Assert.NotEmpty(vm.Rows);
        Assert.All(
            vm.Rows,
            row => Assert.Contains("SRV-SQL01", row.HostName + row.ServiceName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Taken_filter_and_taken_by_search_use_shared_logic()
    {
        var vm = CreateLoaded(TakenSnapshot());
        vm.SelectTakenFilterCommand.Execute(null);
        Assert.True(vm.IsFilterTaken);
        var taken = Assert.Single(vm.Rows);
        Assert.True(taken.ShowTaken);
        Assert.Contains("Michał", taken.TakeStateText, StringComparison.Ordinal);

        vm.SelectAllFilterCommand.Execute(null);
        vm.SearchText = "Michał";
        Assert.Single(vm.Rows);
        Assert.Equal("SRV-SQL01", vm.Rows[0].HostName);
    }

    [Fact]
    public void Seen_toggle_is_local_and_updates_new_count()
    {
        var vm = CreateLoaded();
        var newest = vm.Rows.First(row => row.IsNew);
        var before = vm.NewCount;
        newest.ToggleSeenCommand.Execute(null);
        Assert.True(vm.NewCount < before);
        Assert.DoesNotContain(vm.Rows.Where(row => row.IsNew), row => row.ObjectId.Equals(newest.ObjectId));
    }

    [Fact]
    public void Open_in_checkmk_uses_the_row_object_id()
    {
        var navigator = new FakeNavigator();
        var vm = CreateLoaded(navigator: navigator);
        var row = vm.Rows[0];
        row.OpenInCheckmkCommand.Execute(null);
        Assert.Equal(row.ObjectId, navigator.Last);
    }

    [Fact]
    public void Poller_error_projects_connection_error_without_throwing()
    {
        var poller = new FakePoller();
        var vm = CreateLoaded(poller: poller);
        poller.Status = new ConnectionStatus(ConnectionStatusKind.Error, null, "unreachable");
        poller.Raise();
        Assert.Equal(MacMenuBarConnectionState.Error, vm.ConnectionState);
        Assert.Equal("Connection error", vm.ConnectionLabel);
        Assert.StartsWith("!", vm.MenuBarTitle, StringComparison.Ordinal);
    }

    [Fact]
    public void Unconfigured_startup_does_not_claim_connected()
    {
        var vm = CreateUnconfigured();
        Assert.False(MacStartupPolicy.ShowSettingsOnStartup(false));
        Assert.True(MacStartupPolicy.ShowSettingsOnStartup(true));
        Assert.Equal(MacMenuBarConnectionState.NotConfigured, vm.ConnectionState);
        Assert.Equal("Checkmk", vm.MenuBarTitle);
    }

    private static MacProblemListViewModel CreateLoaded(
        ProblemSnapshot? snapshot = null,
        FakeNavigator? navigator = null,
        FakePoller? poller = null)
    {
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), TimeProvider.System);
        alerts.ApplySnapshot(snapshot ?? DemoSnapshotFactory.Create(DateTimeOffset.UnixEpoch.AddHours(4)));
        poller ??= new FakePoller
        {
            Status = new ConnectionStatus(ConnectionStatusKind.Connected, DateTimeOffset.UnixEpoch, null)
        };
        navigator ??= new FakeNavigator();
        var loaded = new LoadedConfiguration
        {
            Options = new CheckmkOptions { Mode = ClientMode.Real, PollIntervalSeconds = 60 },
            Source = ConfigurationSource.Gui,
            IsUsableReal = true,
            IsMock = false
        };
        var vm = new MacProblemListViewModel(
            alerts,
            poller,
            navigator,
            ImmediateUiThread.Instance,
            loaded);
        vm.StartListening();
        return vm;
    }

    private static ProblemSnapshot TakenSnapshot()
    {
        var now = DateTimeOffset.UnixEpoch.AddHours(4);
        return ProblemSnapshot.Success(now, DemoSnapshotFactory.SiteId,
        [
            new MonitoredProblem
            {
                Id = DemoSnapshotFactory.CpuCriticalId,
                Severity = Severity.Critical,
                StateType = StateType.Hard,
                PluginOutput = "CPU utilization 97.4%",
                LastTimeOk = now.AddHours(-1),
                IsAcknowledgedInCheckmk = true,
                IsTakenByNotifier = true,
                TakenByDisplayName = "Michał"
            },
            new MonitoredProblem
            {
                Id = DemoSnapshotFactory.DiskWarningId,
                Severity = Severity.Warning,
                StateType = StateType.Hard,
                PluginOutput = "disk",
                LastTimeOk = now.AddHours(-2)
            }
        ]);
    }

    private static MacProblemListViewModel CreateUnconfigured()
    {
        var loaded = new LoadedConfiguration
        {
            Options = new CheckmkOptions { Mode = ClientMode.Real, PollIntervalSeconds = 60 },
            Source = ConfigurationSource.None,
            IsUsableReal = false,
            IsMock = false
        };
        return new MacProblemListViewModel(
            new AlertStateService(new InMemoryAlertStateStore(), TimeProvider.System),
            new FakePoller(),
            new FakeNavigator(),
            ImmediateUiThread.Instance,
            loaded);
    }

    private sealed class FakeNavigator : ICheckmkProblemNavigator
    {
        public MonitoredObjectId? Last { get; private set; }

        public CheckmkNavigationResult Open(MonitoredObjectId id)
        {
            Last = id;
            return CheckmkNavigationResult.Succeeded(new Uri("https://checkmk.example.invalid/site/check_mk/"));
        }
    }

    private sealed class FakePoller : IProblemPoller
    {
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Idle;

        public TimeSpan Interval { get; private set; } = TimeSpan.FromSeconds(60);

        public event EventHandler? StateChanged;

        public void Raise() => StateChanged?.Invoke(this, EventArgs.Empty);

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshWhenIdleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RunLoopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void SetInterval(TimeSpan interval) => Interval = interval;
    }
}
