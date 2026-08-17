using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class HostGroupingNotificationCoordinatorTests
{
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Host_down_with_ten_new_services_emits_one_grouped_notification()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(HostDown(), ChildServices(10)));

        var shown = Assert.Single(harness.Notifications.Shown);
        Assert.True(shown.IsGroupedHostFailure);
        Assert.Equal(Severity.Critical, shown.Severity);
        Assert.Equal("HOST DOWN\nSRV-SQL01\n10 affected services", shown.Body);
        Assert.Equal(1, harness.Sound.PlayCount);
        Assert.Equal(11, harness.Alerts.GetOpenIncidents().Count);
        Assert.All(harness.Alerts.GetOpenIncidents(), incident => Assert.False(incident.IsSeen));
    }

    [Fact]
    public void Grouped_notification_plays_one_sound()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(HostDown(), ChildServices(8)));
        Assert.Equal(1, harness.Sound.PlayCount);
        Assert.Single(harness.Notifications.Shown);
    }

    [Fact]
    public void Child_services_remain_new_in_core()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(HostDown(), ChildServices(4)));
        var children = harness.Alerts.GetOpenIncidents()
            .Where(incident => incident.ObjectId.Kind == ObjectKind.Service)
            .ToArray();
        Assert.Equal(4, children.Length);
        Assert.All(children, incident => Assert.False(incident.IsSeen));
        Assert.All(children, incident => Assert.Equal(IncidentStatus.New, incident.Status));
    }

    [Fact]
    public void Same_down_on_next_poll_does_not_repeat()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var problems = Concat(HostDown(), ChildServices(5));
        harness.Apply(Ok(problems));
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(problems));

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
    }

    [Fact]
    public void Host_recurrence_after_recovery_notifies_again()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var firstUp = _clock.UtcNow.AddHours(-3);
        harness.Apply(Ok(HostDown(lastUp: firstUp), ChildServices(3)));
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok());
        Assert.Empty(harness.Alerts.GetOpenIncidents());

        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(HostDown(lastUp: _clock.UtcNow.AddMinutes(-1)), ChildServices(3)));
        var shown = Assert.Single(harness.Notifications.Shown);
        Assert.True(shown.IsGroupedHostFailure);
        Assert.Equal(1, harness.Sound.PlayCount);
    }

    [Fact]
    public void Unreachable_host_emits_one_grouped_unknown_notification()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(HostUnreachable(), ChildServices(6)));

        var shown = Assert.Single(harness.Notifications.Shown);
        Assert.True(shown.IsGroupedHostFailure);
        Assert.Equal(Severity.Unknown, shown.Severity);
        Assert.StartsWith("HOST UNREACHABLE\n", shown.Body, StringComparison.Ordinal);
        Assert.Contains("6 affected services", shown.Body, StringComparison.Ordinal);
        Assert.Equal(1, harness.Sound.PlayCount);
    }

    [Fact]
    public void Service_only_new_without_host_failure_notifies_normally()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Svc("web01", "CPU", Severity.Warning)));

        var shown = Assert.Single(harness.Notifications.Shown);
        Assert.False(shown.IsGroupedHostFailure);
        Assert.Equal(ObjectKind.Service, shown.ObjectId.Kind);
        Assert.Equal(1, harness.Sound.PlayCount);
    }

    [Fact]
    public void Unrelated_host_service_remains_normal()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Concat(HostDown(), ChildServices(2), Svc("OTHER", "Backup", Severity.Critical))));

        Assert.Equal(2, harness.Notifications.Shown.Count);
        Assert.Contains(harness.Notifications.Shown, alert => alert.IsGroupedHostFailure);
        Assert.Contains(harness.Notifications.Shown, alert =>
            !alert.IsGroupedHostFailure && alert.ObjectId.HostName == "OTHER");
        Assert.Equal(2, harness.Sound.PlayCount);
    }

    [Fact]
    public void Pre_existing_service_before_host_down_retains_state()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var existing = Svc("SRV-SQL01", "CPU", Severity.Warning);
        harness.Apply(Ok(existing));
        var before = Assert.Single(harness.Alerts.GetOpenIncidents());
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(HostDown(), existing, Svc("SRV-SQL01", "Memory", Severity.Unknown)));

        var cpu = harness.Alerts.GetOpenIncidents().Single(i => i.ObjectId.ServiceDescription == "CPU");
        Assert.Equal(before.OpenedAtUtc, cpu.OpenedAtUtc);
        Assert.Equal(before.IsSeen, cpu.IsSeen);
        Assert.False(cpu.IsSeen);
        var shown = Assert.Single(harness.Notifications.Shown);
        Assert.True(shown.IsGroupedHostFailure);
        Assert.Equal(1, harness.Sound.PlayCount);
    }

    [Fact]
    public void Seen_child_remains_seen()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var existing = Svc("SRV-SQL01", "CPU", Severity.Warning);
        harness.Apply(Ok(existing));
        harness.Alerts.MarkSeen(existing.Id);
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(HostDown(), existing, Svc("SRV-SQL01", "Memory", Severity.Critical)));

        var cpu = harness.Alerts.GetOpenIncidents().Single(i => i.ObjectId.ServiceDescription == "CPU");
        Assert.True(cpu.IsSeen);
        var memory = harness.Alerts.GetOpenIncidents().Single(i => i.ObjectId.ServiceDescription == "Memory");
        Assert.False(memory.IsSeen);
        Assert.Single(harness.Notifications.Shown);
    }

    [Fact]
    public void Grouping_does_not_mark_seen()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(HostDown(), ChildServices(3)));
        Assert.All(harness.Alerts.GetOpenIncidents(), incident => Assert.False(incident.IsSeen));
        Assert.DoesNotContain(harness.Alerts.GetOpenIncidents(), incident => incident.IsSeen);
    }

    [Fact]
    public void Mute_suppresses_grouped_sound_only()
    {
        var harness = CreateHarness();
        harness.Preferences.SetMuteSound(true);
        harness.BaselineEmpty();
        harness.Apply(Ok(HostDown(), ChildServices(7)));

        Assert.Single(harness.Notifications.Shown);
        Assert.True(harness.Notifications.Shown[0].IsGroupedHostFailure);
        Assert.Equal(0, harness.Sound.PlayCount);
    }

    [Fact]
    public async Task Grouped_notification_backend_failure_does_not_break_polling()
    {
        var notifications = new ThrowingNotificationService();
        var sound = new RecordingAlertSoundService();
        var coordinator = new NotificationCoordinator(notifications, sound, new InMemoryUserPreferences());
        var (poller, _, client) = PollerTestHost.Create(notifications: coordinator);
        client.Snapshot = Ok();
        await poller.RefreshAsync();
        client.Snapshot = Ok(HostDown(), ChildServices(10));

        var exception = await Record.ExceptionAsync(() => poller.RefreshAsync());
        Assert.Null(exception);
        Assert.Equal(ConnectionStatusKind.Connected, poller.Status.Kind);
        Assert.Equal(1, sound.PlayCount);
    }

    [Fact]
    public void Failed_snapshot_produces_no_grouping()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(HostDown(), ChildServices(4)));
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Failed());

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
        Assert.Equal(5, harness.Alerts.GetOpenIncidents().Count);

        var opened = harness.Alerts.GetOpenIncidents();
        harness.Coordinator.Process(Failed(), new AlertDelta(opened, [], []), wasVirginLocalState: false);
        Assert.Empty(harness.Notifications.Shown);
    }

    [Fact]
    public void Services_from_different_site_never_group()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var other = new MonitoredProblem
        {
            Id = MonitoredObjectId.Service(new SiteId("othersite"), "SRV-SQL01", "Remote"),
            Severity = Severity.Critical,
            StateType = StateType.Hard,
            LastTimeOk = _clock.UtcNow.AddHours(-1),
            PluginOutput = "crit"
        };
        harness.Apply(Ok(HostDown(), ChildServices(2).Concat([other]).ToArray()));

        Assert.Equal(2, harness.Notifications.Shown.Count);
        Assert.Contains(harness.Notifications.Shown, alert => alert.IsGroupedHostFailure);
        Assert.Contains(harness.Notifications.Shown, alert => alert.ObjectId.SiteId.Value == "othersite");
    }

    [Fact]
    public void Services_from_different_hostname_never_group()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Concat(HostDown(), ChildServices(2), Svc("web-other", "CPU", Severity.Warning))));
        Assert.Equal(2, harness.Notifications.Shown.Count);
        Assert.Contains(harness.Notifications.Shown, alert => alert.ObjectId.HostName == "web-other");
    }

    [Fact]
    public void Host_ack_or_downtime_does_not_suppress_grouped_notification()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var host = HostDown();
        host = host with { IsAcknowledgedInCheckmk = true, ScheduledDowntimeDepth = 1 };
        harness.Apply(Ok(host, ChildServices(2)));
        Assert.Single(harness.Notifications.Shown);
        Assert.True(harness.Notifications.Shown[0].IsGroupedHostFailure);
        var openHost = harness.Alerts.GetOpenIncidents().Single(i => i.ObjectId.Kind == ObjectKind.Host);
        Assert.True(openHost.IsAcknowledgedInCheckmk);
        Assert.Equal(1, openHost.ScheduledDowntimeDepth);
        Assert.False(openHost.IsSeen);
    }

    [Fact]
    public void New_children_while_host_already_down_do_not_storm()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var host = HostDown(_clock.UtcNow.AddHours(-2));
        harness.Apply(Ok(host));
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(host, ChildServices(12)));

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
        Assert.Equal(12, harness.Alerts.GetOpenIncidents().Count(i => i.ObjectId.Kind == ObjectKind.Service));
        Assert.All(
            harness.Alerts.GetOpenIncidents().Where(i => i.ObjectId.Kind == ObjectKind.Service),
            incident => Assert.False(incident.IsSeen));
    }

    private Harness CreateHarness(IAlertStateStore? store = null) => new(_clock, store);

    private ProblemSnapshot Ok(params MonitoredProblem[] problems) =>
        ProblemSnapshot.Success(_clock.UtcNow, new SiteId("itssrv"), problems);

    private ProblemSnapshot Ok(MonitoredProblem host, MonitoredProblem[] services) =>
        Ok(Concat(host, services));

    private ProblemSnapshot Failed() =>
        ProblemSnapshot.Failure(_clock.UtcNow, SnapshotErrorKind.Unavailable, "Checkmk unreachable");

    private static MonitoredProblem[] Concat(MonitoredProblem host, MonitoredProblem[] services) =>
        new[] { host }.Concat(services).ToArray();

    private static MonitoredProblem[] Concat(MonitoredProblem host, MonitoredProblem[] services, MonitoredProblem extra) =>
        new[] { host }.Concat(services).Append(extra).ToArray();

    private MonitoredProblem HostDown(DateTimeOffset? lastUp = null) =>
        new()
        {
            Id = MonitoredObjectId.Host(new SiteId("itssrv"), "SRV-SQL01"),
            Severity = Severity.Critical,
            StateType = StateType.Hard,
            PluginOutput = "DOWN",
            LastTimeUp = lastUp ?? _clock.UtcNow.AddHours(-2)
        };

    private MonitoredProblem HostUnreachable() =>
        new()
        {
            Id = MonitoredObjectId.Host(new SiteId("itssrv"), "SRV-SQL01"),
            Severity = Severity.Unknown,
            StateType = StateType.Hard,
            PluginOutput = "UNREACHABLE",
            LastTimeUp = _clock.UtcNow.AddHours(-2)
        };

    private MonitoredProblem[] ChildServices(int count) =>
        Enumerable.Range(1, count)
            .Select(i => Svc("SRV-SQL01", $"svc{i}", i % 2 == 0 ? Severity.Warning : Severity.Critical))
            .ToArray();

    private MonitoredProblem Svc(string host, string service, Severity severity) =>
        new()
        {
            Id = MonitoredObjectId.Service(new SiteId("itssrv"), host, service),
            Severity = severity,
            StateType = StateType.Hard,
            PluginOutput = service,
            LastTimeOk = _clock.UtcNow.AddHours(-1)
        };

    private sealed class Harness
    {
        public Harness(MutableClock clock, IAlertStateStore? store)
        {
            Clock = clock;
            Alerts = new AlertStateService(store ?? new InMemoryAlertStateStore(), clock);
            Notifications = new RecordingNotificationService();
            Sound = new RecordingAlertSoundService();
            Preferences = new InMemoryUserPreferences();
            Coordinator = new NotificationCoordinator(Notifications, Sound, Preferences);
        }

        public MutableClock Clock { get; }
        public AlertStateService Alerts { get; }
        public RecordingNotificationService Notifications { get; }
        public RecordingAlertSoundService Sound { get; }
        public InMemoryUserPreferences Preferences { get; }
        public NotificationCoordinator Coordinator { get; }

        public void BaselineEmpty() => Apply(ProblemSnapshot.Success(Clock.UtcNow, new SiteId("itssrv"), []));

        public void Apply(ProblemSnapshot snapshot)
        {
            var virgin = NotificationBaseline.IsVirginLocalState(
                Alerts.GetOpenIncidents().Count,
                Alerts.LastSuccessfulPollUtc);
            var delta = Alerts.ApplySnapshot(snapshot);
            Coordinator.Process(snapshot, delta, virgin);
        }
    }
}
