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

    [Fact]
    public async Task Successful_service_release_deletes_and_confirms_after_refresh()
    {
        var harness = CreateHarness();
        harness.AckOpenService();
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () =>
        {
            if (harness.Acks.ServiceDeletes > 0)
            {
                harness.ClearAck();
            }
        };

        var result = await harness.Sut.ReleaseAsync(ProblemId());

        Assert.Equal(TakeOperationStatus.Confirmed, result.Status);
        Assert.Equal(1, harness.Acks.ServiceDeletes);
        Assert.Equal(0, harness.Acks.HostDeletes);
        Assert.Equal(2, harness.Poller.RefreshCount);
        Assert.Equal("web01", harness.Acks.LastHost);
        Assert.Equal("CPU", harness.Acks.LastService);
        var open = Assert.Single(harness.Alerts.GetOpenIncidents());
        Assert.False(open.IsTakenByNotifier);
        Assert.Null(open.TakenByDisplayName);
        Assert.False(open.IsAcknowledgedInCheckmk);
    }

    [Fact]
    public async Task Successful_host_release_deletes_host_only()
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
                    StateType = StateType.Hard,
                    IsAcknowledgedInCheckmk = true,
                    IsTakenByNotifier = true,
                    TakenByDisplayName = "Michał"
                }
            ]));
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () =>
        {
            if (harness.Acks.HostDeletes > 0)
            {
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
            }
        };

        var result = await harness.Sut.ReleaseAsync(hostId);
        Assert.Equal(TakeOperationStatus.Confirmed, result.Status);
        Assert.Equal(1, harness.Acks.HostDeletes);
        Assert.Equal(0, harness.Acks.ServiceDeletes);
    }

    [Fact]
    public async Task Generic_ack_cannot_release()
    {
        var harness = CreateHarness();
        harness.OpenGenericAck();
        var result = await harness.Sut.ReleaseAsync(ProblemId());
        Assert.Equal(TakeOperationStatus.NotEligible, result.Status);
        Assert.Equal(0, harness.Acks.ServiceDeletes);
        Assert.Equal(0, harness.Acks.HostDeletes);
        Assert.True(Assert.Single(harness.Alerts.GetOpenIncidents()).IsAcknowledgedInCheckmk);
    }

    [Fact]
    public async Task Another_admins_cdn_take_can_release()
    {
        var harness = CreateHarness();
        harness.AckOpenService("Paweł");
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () =>
        {
            if (harness.Acks.ServiceDeletes > 0)
            {
                harness.ClearAck();
            }
        };

        var result = await harness.Sut.ReleaseAsync(ProblemId());
        Assert.Equal(TakeOperationStatus.Confirmed, result.Status);
        Assert.Equal(1, harness.Acks.ServiceDeletes);
    }

    [Fact]
    public async Task Release_does_not_change_seen()
    {
        var harness = CreateHarness();
        harness.AckOpenService();
        harness.Alerts.MarkSeen(ProblemId());
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () =>
        {
            if (harness.Acks.ServiceDeletes > 0)
            {
                harness.ClearAck();
            }
        };

        await harness.Sut.ReleaseAsync(ProblemId());
        Assert.True(Assert.Single(harness.Alerts.GetOpenIncidents()).IsSeen);
    }

    [Fact]
    public async Task Release_does_not_change_unseen()
    {
        var harness = CreateHarness();
        harness.AckOpenService();
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () =>
        {
            if (harness.Acks.ServiceDeletes > 0)
            {
                harness.ClearAck();
            }
        };

        await harness.Sut.ReleaseAsync(ProblemId());
        Assert.False(Assert.Single(harness.Alerts.GetOpenIncidents()).IsSeen);
    }

    [Fact]
    public async Task Successful_delete_with_failed_refresh_does_not_invent_released_state()
    {
        var harness = CreateHarness();
        harness.AckOpenService();
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        var refreshes = 0;
        harness.Poller.OnRefresh = () =>
        {
            refreshes++;
            if (refreshes > 1)
            {
                throw new InvalidOperationException("refresh failed");
            }
        };

        var result = await harness.Sut.ReleaseAsync(ProblemId());
        Assert.Equal(TakeOperationStatus.SentAwaitingRefresh, result.Status);
        var open = Assert.Single(harness.Alerts.GetOpenIncidents());
        Assert.True(open.IsTakenByNotifier);
        Assert.Equal("Michał", open.TakenByDisplayName);
    }

    [Fact]
    public async Task Concurrent_no_longer_taken_is_confirmed_without_delete_when_precheck_fails()
    {
        var harness = CreateHarness();
        harness.AckOpenService();
        harness.Poller.OnRefresh = () => harness.ClearAck();

        var result = await harness.Sut.ReleaseAsync(ProblemId());
        Assert.Equal(TakeOperationStatus.NotEligible, result.Status);
        Assert.Equal(0, harness.Acks.ServiceDeletes);
        Assert.False(Assert.Single(harness.Alerts.GetOpenIncidents()).IsTakenByNotifier);
    }

    [Fact]
    public async Task Concurrent_400_after_delete_confirms_when_refresh_shows_released()
    {
        var harness = CreateHarness();
        harness.AckOpenService();
        harness.Acks.Next = AcknowledgementWriteResult.InvalidRequest;
        harness.Poller.OnRefresh = () =>
        {
            if (harness.Acks.ServiceDeletes > 0)
            {
                harness.ClearAck();
            }
        };

        var result = await harness.Sut.ReleaseAsync(ProblemId());
        Assert.Equal(TakeOperationStatus.Confirmed, result.Status);
        Assert.Equal(1, harness.Acks.ServiceDeletes);
    }

    [Theory]
    [InlineData(AcknowledgementWriteStatus.Unauthorized, TakeOperationStatus.Unauthorized)]
    [InlineData(AcknowledgementWriteStatus.Forbidden, TakeOperationStatus.Forbidden)]
    [InlineData(AcknowledgementWriteStatus.Unavailable, TakeOperationStatus.Unavailable)]
    public async Task Release_maps_write_failures(
        AcknowledgementWriteStatus write,
        TakeOperationStatus expected)
    {
        var harness = CreateHarness();
        harness.AckOpenService();
        harness.Acks.Next = write switch
        {
            AcknowledgementWriteStatus.Unauthorized => AcknowledgementWriteResult.Unauthorized,
            AcknowledgementWriteStatus.Forbidden => AcknowledgementWriteResult.Forbidden,
            _ => AcknowledgementWriteResult.Unavailable
        };

        var result = await harness.Sut.ReleaseAsync(ProblemId());
        Assert.Equal(expected, result.Status);
        Assert.Equal(1, harness.Acks.ServiceDeletes);
        Assert.True(Assert.Single(harness.Alerts.GetOpenIncidents()).IsTakenByNotifier);
        if (expected == TakeOperationStatus.Forbidden)
        {
            Assert.True(harness.Session.AcknowledgeForbidden);
        }
    }

    [Fact]
    public async Task Feature_disabled_still_releases_cdn_take()
    {
        var harness = CreateHarness();
        harness.Preferences.SetTakeEnabled(false);
        harness.AckOpenService();
        harness.Acks.Next = AcknowledgementWriteResult.Success;
        harness.Poller.OnRefresh = () =>
        {
            if (harness.Acks.ServiceDeletes > 0)
            {
                harness.ClearAck();
            }
        };

        var result = await harness.Sut.ReleaseAsync(ProblemId());
        Assert.Equal(TakeOperationStatus.Confirmed, result.Status);
        Assert.Equal(1, harness.Acks.ServiceDeletes);
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

        public void AckOpenService(string takenBy = "Michał")
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
                        TakenByDisplayName = takenBy
                    }
                ]));
        }

        public void OpenGenericAck()
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
                        AcknowledgementType = AcknowledgementType.Sticky
                    }
                ]));
        }

        public void ClearAck(bool seen = false)
        {
            var existing = Alerts.GetOpenIncidents().FirstOrDefault(incident => incident.ObjectId.Equals(ProblemId()));
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
            if (seen || existing?.IsSeen == true)
            {
                Alerts.MarkSeen(ProblemId());
            }
        }
    }

    private sealed class FakeAcknowledgementClient : ICheckmkAcknowledgementClient
    {
        public AcknowledgementWriteResult Next { get; set; } = AcknowledgementWriteResult.Success;
        public int ServiceCalls { get; private set; }
        public int HostCalls { get; private set; }
        public int ServiceDeletes { get; private set; }
        public int HostDeletes { get; private set; }
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

        public Task<AcknowledgementWriteResult> DeleteHostAsync(
            string hostName,
            CancellationToken cancellationToken = default)
        {
            HostDeletes++;
            LastHost = hostName;
            LastService = null;
            return Task.FromResult(Next);
        }

        public Task<AcknowledgementWriteResult> DeleteServiceAsync(
            string hostName,
            string serviceDescription,
            CancellationToken cancellationToken = default)
        {
            ServiceDeletes++;
            LastHost = hostName;
            LastService = serviceDescription;
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
