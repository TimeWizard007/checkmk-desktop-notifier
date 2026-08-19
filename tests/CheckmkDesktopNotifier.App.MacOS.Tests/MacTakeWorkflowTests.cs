using CheckmkDesktopNotifier.App.MacOS;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Mock;
using CheckmkDesktopNotifier.Core.Navigation;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Rest;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class MacTakeWorkflowTests
{
    [Fact]
    public async Task Take_read_back_shows_taken_not_optimistic_final_state()
    {
        var harness = Create();
        harness.OpenUnacked();
        var vm = harness.CreateVm();
        vm.Confirm = (_, _, _) => Task.FromResult<bool?>(true);
        var row = vm.Rows.Single(item => item.ObjectId.Equals(ProblemId()));
        Assert.True(row.ShowTake);
        Assert.False(row.ShowTaken);

        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = harness.AckAsTake;
        row.TakeCommand.Execute(null);
        await WaitUntil(() => vm.Rows.Any(item => item.ShowTaken));

        var taken = vm.Rows.Single(item => item.ObjectId.Equals(ProblemId()));
        Assert.True(taken.ShowTaken);
        Assert.Contains("Michał", taken.TakeStateText, StringComparison.Ordinal);
        Assert.True(taken.CanRelease);
        Assert.Equal(1, harness.Acks.ServiceCalls);
    }

    [Fact]
    public async Task Taken_release_read_back_restores_take()
    {
        var harness = Create();
        harness.AckAsTake();
        var vm = harness.CreateVm();
        vm.Confirm = (_, _, _) => Task.FromResult<bool?>(true);
        var row = vm.Rows.Single();
        Assert.True(row.ShowTaken);
        Assert.True(row.CanRelease);

        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () =>
        {
            if (harness.Acks.ServiceDeletes > 0)
            {
                harness.OpenUnacked();
            }
        };
        row.ReleaseCommand.Execute(null);
        await WaitUntil(() => vm.Rows.Any(item => item.ShowTake));

        var available = vm.Rows.Single();
        Assert.True(available.ShowTake);
        Assert.False(available.ShowTaken);
        Assert.Equal(1, harness.Acks.ServiceDeletes);
    }

    [Fact]
    public void Generic_ack_cannot_be_released()
    {
        var harness = Create();
        harness.OpenGenericAck();
        var vm = harness.CreateVm();
        var row = Assert.Single(vm.Rows);
        Assert.True(row.ShowAck);
        Assert.False(row.CanRelease);
        Assert.False(row.ShowTake);
        Assert.False(row.ShowTaken);
    }

    [Fact]
    public async Task Take_failure_does_not_mark_taken()
    {
        var harness = Create();
        harness.OpenUnacked();
        var vm = harness.CreateVm();
        vm.Confirm = (_, _, _) => Task.FromResult<bool?>(true);
        harness.Acks.Next = AcknowledgementWriteResult.Forbidden;
        vm.Rows.Single().TakeCommand.Execute(null);
        await WaitUntil(() => vm.HasError);

        Assert.False(vm.Rows.Single().ShowTaken);
        Assert.Contains("cannot acknowledge", vm.ErrorText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, harness.Poller.RefreshCount);
    }

    [Fact]
    public async Task Successful_write_without_refresh_keeps_waiting_not_taken()
    {
        var harness = Create();
        harness.OpenUnacked();
        var vm = harness.CreateVm();
        vm.Confirm = (_, _, _) => Task.FromResult<bool?>(true);
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.ThrowOnRefresh = true;
        vm.Rows.Single().TakeCommand.Execute(null);
        await WaitUntil(() => vm.Rows.Any(item => item.ShowTaking));

        Assert.False(vm.Rows.Single().ShowTaken);
        Assert.True(vm.Rows.Single().ShowTaking);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void Cancelled_confirmation_does_not_write()
    {
        var harness = Create();
        harness.OpenUnacked();
        var vm = harness.CreateVm();
        vm.Confirm = (_, _, _) => Task.FromResult<bool?>(false);
        vm.Rows.Single().TakeCommand.Execute(null);
        Assert.Equal(0, harness.Acks.ServiceCalls);
        Assert.True(vm.Rows.Single().ShowTake);
    }

    [Fact]
    public void Poll_refresh_keeps_filter_and_does_not_duplicate_rows()
    {
        var harness = Create();
        harness.OpenUnacked();
        var vm = harness.CreateVm();
        vm.SelectCriticalFilterCommand.Execute(null);
        var before = vm.Rows.Count;
        harness.Poller.Raise();
        Assert.True(vm.IsFilterCritical);
        Assert.Equal(before, vm.Rows.Count);
    }

    private static Harness Create() => new();

    private static MonitoredObjectId ProblemId() => DemoSnapshotFactory.CpuCriticalId;

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private sealed class Harness
    {
        public Harness()
        {
            Alerts = new AlertStateService(new InMemoryAlertStateStore(), TimeProvider.System);
            Preferences = new MemoryPreferences();
            Preferences.SetTakeEnabled(true);
            Preferences.SetTakeDisplayName("Michał");
            Session = new TakeSessionState();
            Acks = new FakeAcknowledgementClient();
            Poller = new FakePoller();
            Take = new CheckmkTakeService(Acks, Poller, Alerts, Preferences, Session);
        }

        public AlertStateService Alerts { get; }
        public MemoryPreferences Preferences { get; }
        public TakeSessionState Session { get; }
        public FakeAcknowledgementClient Acks { get; }
        public FakePoller Poller { get; }
        public CheckmkTakeService Take { get; }

        public MacProblemListViewModel CreateVm()
        {
            var loaded = new LoadedConfiguration
            {
                Options = new CheckmkOptions { Mode = ClientMode.Real, PollIntervalSeconds = 60 },
                Source = ConfigurationSource.Gui,
                IsUsableReal = true,
                IsMock = false
            };
            var vm = new MacProblemListViewModel(
                Alerts,
                Poller,
                new FakeNavigator(),
                ImmediateUiThread.Instance,
                loaded,
                take: Take,
                preferences: Preferences,
                takeSession: Session);
            vm.StartListening();
            return vm;
        }

        public void OpenUnacked()
        {
            Alerts.ApplySnapshot(ProblemSnapshot.Success(
                DateTimeOffset.UtcNow,
                DemoSnapshotFactory.SiteId,
                [
                    new MonitoredProblem
                    {
                        Id = ProblemId(),
                        Severity = Severity.Critical,
                        StateType = StateType.Hard,
                        PluginOutput = "CPU"
                    }
                ]));
        }

        public void AckAsTake()
        {
            Alerts.ApplySnapshot(ProblemSnapshot.Success(
                DateTimeOffset.UtcNow,
                DemoSnapshotFactory.SiteId,
                [
                    new MonitoredProblem
                    {
                        Id = ProblemId(),
                        Severity = Severity.Critical,
                        StateType = StateType.Hard,
                        PluginOutput = "CPU",
                        IsAcknowledgedInCheckmk = true,
                        IsTakenByNotifier = true,
                        TakenByDisplayName = "Michał"
                    }
                ]));
        }

        public void OpenGenericAck()
        {
            Alerts.ApplySnapshot(ProblemSnapshot.Success(
                DateTimeOffset.UtcNow,
                DemoSnapshotFactory.SiteId,
                [
                    new MonitoredProblem
                    {
                        Id = ProblemId(),
                        Severity = Severity.Critical,
                        StateType = StateType.Hard,
                        IsAcknowledgedInCheckmk = true,
                        AcknowledgementType = AcknowledgementType.Sticky
                    }
                ]));
        }
    }

    private sealed class MemoryPreferences : IUserPreferences
    {
        public bool MuteSound { get; private set; }
        public int VolumePercent { get; private set; } = 30;
        public NotificationSoundSource SoundSource { get; private set; }
        public string? CustomSoundFileName { get; private set; }
        public bool TakeEnabled { get; private set; }
        public string? TakeDisplayName { get; private set; }
        public event EventHandler? Changed;
        public void SetMuteSound(bool mute) => MuteSound = mute;
        public void SetVolumePercent(int volumePercent) => VolumePercent = volumePercent;
        public void SetSoundSource(NotificationSoundSource source) => SoundSource = source;
        public void SetCustomSoundFileName(string? fileName) => CustomSoundFileName = fileName;
        public void SetTakeEnabled(bool enabled)
        {
            TakeEnabled = enabled;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        public void SetTakeDisplayName(string? displayName) => TakeDisplayName = displayName;
    }

    private sealed class FakeAcknowledgementClient : ICheckmkAcknowledgementClient
    {
        public AcknowledgementWriteResult Next { get; set; } = AcknowledgementWriteResult.Success;
        public int ServiceCalls { get; private set; }
        public int ServiceDeletes { get; private set; }

        public Task<AcknowledgementWriteResult> AcknowledgeHostAsync(
            string hostName, string displayName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Next);

        public Task<AcknowledgementWriteResult> AcknowledgeServiceAsync(
            string hostName, string serviceDescription, string displayName, CancellationToken cancellationToken = default)
        {
            ServiceCalls++;
            return Task.FromResult(Next);
        }

        public Task<AcknowledgementWriteResult> DeleteHostAsync(
            string hostName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Next);

        public Task<AcknowledgementWriteResult> DeleteServiceAsync(
            string hostName, string serviceDescription, CancellationToken cancellationToken = default)
        {
            ServiceDeletes++;
            return Task.FromResult(Next);
        }
    }

    private sealed class FakePoller : IProblemPoller
    {
        public int RefreshCount { get; private set; }
        public bool ThrowOnRefresh { get; set; }
        public Action? OnRefresh { get; set; }
        public ConnectionStatus Status { get; set; } =
            new(ConnectionStatusKind.Connected, DateTimeOffset.UtcNow, null);
        public TimeSpan Interval { get; private set; } = TimeSpan.FromSeconds(60);
        public event EventHandler? StateChanged;
        public void Raise() => StateChanged?.Invoke(this, EventArgs.Empty);
        public Task RefreshAsync(CancellationToken cancellationToken = default) => RefreshWhenIdleAsync(cancellationToken);
        public Task RefreshWhenIdleAsync(CancellationToken cancellationToken = default)
        {
            RefreshCount++;
            if (ThrowOnRefresh)
            {
                throw new InvalidOperationException("refresh failed");
            }

            OnRefresh?.Invoke();
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
        public Task RunLoopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SetInterval(TimeSpan interval) => Interval = interval;
    }

    private sealed class FakeNavigator : ICheckmkProblemNavigator
    {
        public CheckmkNavigationResult Open(MonitoredObjectId id) =>
            CheckmkNavigationResult.Succeeded(new Uri("https://checkmk.example.invalid/"));
    }
}
