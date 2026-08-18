using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Polling;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkTakeServiceTests
{
    [Fact]
    public async Task Successful_take_requests_refresh_and_confirms_after_ack()
    {
        var harness = CreateHarness();
        harness.OpenUnackedService();
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () => harness.AckOpenService();

        var result = await harness.Sut.TakeAsync(ProblemId());

        Assert.Equal(TakeOperationStatus.Confirmed, result.Status);
        Assert.Equal(1, harness.Acks.ServiceCalls);
        Assert.Equal(0, harness.Acks.HostCalls);
        Assert.Equal(1, harness.Poller.RefreshCount);
        Assert.Equal("web01", harness.Acks.LastHost);
        Assert.Equal("CPU", harness.Acks.LastService);
        Assert.Equal("Michał", harness.Acks.LastDisplayName);
    }

    [Fact]
    public async Task Successful_write_with_failed_refresh_awaits_checkmk()
    {
        var harness = CreateHarness();
        harness.OpenUnackedService();
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.ThrowOnRefresh = true;

        var result = await harness.Sut.TakeAsync(ProblemId());

        Assert.Equal(TakeOperationStatus.SentAwaitingRefresh, result.Status);
        Assert.False(harness.Alerts.GetOpenIncidents()[0].IsAcknowledgedInCheckmk);
    }

    [Fact]
    public async Task Forbidden_disables_take_for_the_session()
    {
        var harness = CreateHarness();
        harness.OpenUnackedService();
        harness.Acks.Next = AcknowledgementWriteResult.Forbidden;

        var result = await harness.Sut.TakeAsync(ProblemId());

        Assert.Equal(TakeOperationStatus.Forbidden, result.Status);
        Assert.True(harness.Session.AcknowledgeForbidden);
        Assert.Equal(0, harness.Poller.RefreshCount);
    }

    [Fact]
    public async Task Feature_disabled_does_not_write()
    {
        var harness = CreateHarness();
        harness.Preferences.SetTakeEnabled(false);
        var result = await harness.Sut.TakeAsync(ProblemId());
        Assert.Equal(TakeOperationStatus.FeatureDisabled, result.Status);
        Assert.Equal(0, harness.Acks.ServiceCalls);
    }

    [Fact]
    public async Task Host_take_acks_host_only()
    {
        var harness = CreateHarness();
        var hostId = MonitoredObjectId.Host(new SiteId("itssrv"), "web01");
        harness.Alerts.ApplySnapshot(ProblemSnapshot.Success(
            DateTimeOffset.UtcNow,
            new SiteId("itssrv"),
            [
                new MonitoredProblem
                {
                    Id = hostId,
                    Severity = Severity.Critical,
                    StateType = StateType.Hard
                }
            ]));
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () =>
        {
            harness.Alerts.ApplySnapshot(ProblemSnapshot.Success(
                DateTimeOffset.UtcNow,
                new SiteId("itssrv"),
                [
                    new MonitoredProblem
                    {
                        Id = hostId,
                        Severity = Severity.Critical,
                        StateType = StateType.Hard,
                        IsAcknowledgedInCheckmk = true,
                        IsTakenByNotifier = true,
                        TakenByDisplayName = "Michał"
                    }
                ]));
        };

        var result = await harness.Sut.TakeAsync(hostId);
        Assert.Equal(TakeOperationStatus.Confirmed, result.Status);
        Assert.Equal(1, harness.Acks.HostCalls);
        Assert.Equal(0, harness.Acks.ServiceCalls);
    }

    private static MonitoredObjectId ProblemId() =>
        MonitoredObjectId.Service(new SiteId("itssrv"), "web01", "CPU");

    private static Harness CreateHarness() => new();

    private sealed class Harness
    {
        public Harness()
        {
            Alerts = new AlertStateService(new InMemoryAlertStateStore());
            Preferences = new InMemoryUserPreferences();
            Preferences.SetTakeEnabled(true);
            Preferences.SetTakeDisplayName("Michał");
            Acks = new FakeAcknowledgementClient();
            Poller = new FakePoller();
            Session = new TakeSessionState();
            Sut = new CheckmkTakeService(Acks, Poller, Alerts, Preferences, Session);
        }

        public AlertStateService Alerts { get; }
        public InMemoryUserPreferences Preferences { get; }
        public FakeAcknowledgementClient Acks { get; }
        public FakePoller Poller { get; }
        public TakeSessionState Session { get; }
        public CheckmkTakeService Sut { get; }

        public void OpenUnackedService()
        {
            Alerts.ApplySnapshot(ProblemSnapshot.Success(
                DateTimeOffset.UtcNow,
                new SiteId("itssrv"),
                [
                    new MonitoredProblem
                    {
                        Id = ProblemId(),
                        Severity = Severity.Critical,
                        StateType = StateType.Hard
                    }
                ]));
        }

        public void AckOpenService()
        {
            Alerts.ApplySnapshot(ProblemSnapshot.Success(
                DateTimeOffset.UtcNow,
                new SiteId("itssrv"),
                [
                    new MonitoredProblem
                    {
                        Id = ProblemId(),
                        Severity = Severity.Critical,
                        StateType = StateType.Hard,
                        IsAcknowledgedInCheckmk = true,
                        AcknowledgementType = AcknowledgementType.Sticky,
                        IsTakenByNotifier = true,
                        TakenByDisplayName = "Michał"
                    }
                ]));
        }
    }

    private sealed class FakeAcknowledgementClient : ICheckmkAcknowledgementClient
    {
        public AcknowledgementWriteResult Next { get; set; } = AcknowledgementWriteResult.Success;
        public int ServiceCalls { get; private set; }
        public int HostCalls { get; private set; }
        public string? LastHost { get; private set; }
        public string? LastService { get; private set; }
        public string? LastDisplayName { get; private set; }

        public Task<AcknowledgementWriteResult> AcknowledgeHostAsync(
            string hostName,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            HostCalls++;
            LastHost = hostName;
            LastDisplayName = displayName;
            return Task.FromResult(Next);
        }

        public Task<AcknowledgementWriteResult> AcknowledgeServiceAsync(
            string hostName,
            string serviceDescription,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            ServiceCalls++;
            LastHost = hostName;
            LastService = serviceDescription;
            LastDisplayName = displayName;
            return Task.FromResult(Next);
        }
    }

    private sealed class FakePoller : IProblemPoller
    {
        public int RefreshCount { get; private set; }
        public bool ThrowOnRefresh { get; set; }
        public Action? OnRefresh { get; set; }

        public ConnectionStatus Status { get; } = ConnectionStatus.Idle;

        public TimeSpan Interval { get; private set; } = TimeSpan.FromSeconds(60);

        public event EventHandler? StateChanged;

        public Task RefreshAsync(CancellationToken cancellationToken = default) =>
            RefreshWhenIdleAsync(cancellationToken);

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
}
