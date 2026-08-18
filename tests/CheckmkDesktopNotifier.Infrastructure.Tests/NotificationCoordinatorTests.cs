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

public sealed class NotificationCoordinatorTests
{
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void New_incident_emits_notification()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("web01", "CPU", "80%")));

        var shown = Assert.Single(harness.Notifications.Shown);
        Assert.Equal(Severity.Warning, shown.Severity);
        Assert.Equal(1, harness.Sound.PlayCount);
    }

    [Fact]
    public void Same_new_incident_on_next_poll_does_not_notify_again()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var problem = Warn("web01", "CPU", "80%");
        harness.Apply(Ok(problem));
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(problem));

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
    }

    [Fact]
    public void Seen_incident_does_not_notify()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var problem = Crit("web01", "CPU", "99%");
        harness.Apply(Ok(problem));
        harness.Alerts.MarkSeen(problem.Id);
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(problem));

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
    }

    [Fact]
    public void Persisted_seen_after_restart_does_not_notify()
    {
        var store = new InMemoryAlertStateStore();
        var first = CreateHarness(store);
        first.BaselineEmpty();
        var problem = Crit("web01", "CPU", "99%");
        first.Apply(Ok(problem));
        first.Alerts.MarkSeen(problem.Id);

        var restarted = CreateHarness(store);
        restarted.Apply(Ok(problem));

        Assert.Empty(restarted.Notifications.Shown);
        Assert.Equal(0, restarted.Sound.PlayCount);
        Assert.True(Assert.Single(restarted.Alerts.GetOpenIncidents()).IsSeen);
    }

    [Fact]
    public void Mark_unseen_does_not_emit_balloon_or_sound()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var problem = Crit("web01", "CPU", "99%");
        harness.Apply(Ok(problem));
        harness.Alerts.MarkSeen(problem.Id);
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();

        harness.Alerts.MarkUnseen(problem.Id);
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(problem));

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
        Assert.False(Assert.Single(harness.Alerts.GetOpenIncidents()).IsSeen);
    }

    [Fact]
    public void Repeated_seen_unseen_does_not_replay_notification()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var problem = Crit("web01", "CPU", "99%");
        harness.Apply(Ok(problem));
        Assert.Single(harness.Notifications.Shown);
        Assert.Equal(1, harness.Sound.PlayCount);

        for (var i = 0; i < 4; i++)
        {
            harness.Alerts.MarkSeen(problem.Id);
            harness.Alerts.MarkUnseen(problem.Id);
            harness.Alerts.MarkSeen(problem.Id);
            harness.Notifications.Shown.Clear();
            harness.Sound.Reset();
            _clock.Advance(TimeSpan.FromMinutes(1));
            harness.Apply(Ok(problem));
            Assert.Empty(harness.Notifications.Shown);
            Assert.Equal(0, harness.Sound.PlayCount);
        }
    }

    [Fact]
    public void Recovery_then_recurrence_emits_new_notification()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        var first = Crit("web01", "CPU", "99%", lastOk: _clock.UtcNow.AddHours(-2));
        harness.Apply(Ok(first));
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok());
        Assert.Empty(harness.Notifications.Shown);

        _clock.Advance(TimeSpan.FromMinutes(1));
        var again = Crit("web01", "CPU", "98%", lastOk: _clock.UtcNow.AddMinutes(-1));
        harness.Apply(Ok(again));

        Assert.Single(harness.Notifications.Shown);
        Assert.Equal(1, harness.Sound.PlayCount);
    }

    [Fact]
    public void Warn_crit_and_unknown_each_notify_once()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("h1", "Disk", "warn")));
        harness.Apply(Ok(
            Warn("h1", "Disk", "warn"),
            Crit("h1", "CPU", "crit"),
            Unknown("h1", "Agent", "unk")));

        Assert.Equal(3, harness.Notifications.Shown.Count);
        Assert.Contains(harness.Notifications.Shown, alert => alert.Severity == Severity.Warning);
        Assert.Contains(harness.Notifications.Shown, alert => alert.Severity == Severity.Critical);
        Assert.Contains(harness.Notifications.Shown, alert => alert.Severity == Severity.Unknown);
        Assert.Equal(3, harness.Sound.PlayCount);
    }

    [Fact]
    public void Failed_poll_emits_no_notification()
    {
        var harness = CreateHarness();
        harness.Apply(Failed());
        Assert.Empty(harness.Notifications.Shown);
        Assert.Null(harness.Alerts.LastSuccessfulPollUtc);

        var problems = Enumerable.Range(1, 5)
            .Select(i => Warn("web01", $"svc{i}", "x"))
            .ToArray();
        harness.Apply(Ok(problems));
        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(5, harness.Alerts.GetOpenIncidents().Count);

        harness.Notifications.Shown.Clear();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Failed());
        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(5, harness.Alerts.GetOpenIncidents().Count);
    }

    [Fact]
    public void Opened_acknowledged_incident_does_not_notify_or_play_sound()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("web01", "CPU", "80%", acknowledged: true)));

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
        var open = Assert.Single(harness.Alerts.GetOpenIncidents());
        Assert.False(open.IsSeen);
        Assert.True(open.IsAcknowledgedInCheckmk);
    }

    [Fact]
    public void Opened_unacknowledged_incident_notifies()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("web01", "CPU", "80%")));

        Assert.Single(harness.Notifications.Shown);
        Assert.Equal(1, harness.Sound.PlayCount);
        Assert.False(Assert.Single(harness.Alerts.GetOpenIncidents()).IsSeen);
    }

    [Fact]
    public void Ack_appearing_after_already_open_incident_does_not_notify_again()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("web01", "CPU", "80%")));
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();

        harness.Apply(Ok(Warn("web01", "CPU", "80%", acknowledged: true)));
        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
        var open = Assert.Single(harness.Alerts.GetOpenIncidents());
        Assert.False(open.IsSeen);
        Assert.True(open.IsAcknowledgedInCheckmk);
    }

    [Fact]
    public void Downtime_metadata_does_not_imply_seen()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("web01", "CPU", "80%", downtime: 1)));

        Assert.Single(harness.Notifications.Shown);
        var open = Assert.Single(harness.Alerts.GetOpenIncidents());
        Assert.False(open.IsSeen);
        Assert.Equal(1, open.ScheduledDowntimeDepth);
    }

    [Fact]
    public void Mute_disables_sound_but_not_visual_notification()
    {
        var harness = CreateHarness();
        harness.Preferences.SetMuteSound(true);
        harness.BaselineEmpty();
        harness.Apply(Ok(Crit("web01", "CPU", "99%")));

        Assert.Single(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
    }

    [Fact]
    public void Unmute_restores_sound()
    {
        var harness = CreateHarness();
        harness.Preferences.SetMuteSound(true);
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("web01", "Disk", "80%")));
        Assert.Equal(0, harness.Sound.PlayCount);

        MuteCommands.Toggle(harness.Preferences);
        Assert.False(harness.Preferences.MuteSound);
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(
            Warn("web01", "Disk", "80%"),
            Crit("web01", "CPU", "99%")));

        Assert.Equal(1, harness.Sound.PlayCount);
        Assert.Equal(2, harness.Notifications.Shown.Count);
    }

    [Fact]
    public void Warn_to_crit_same_incident_does_not_notify_again()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("web01", "CPU", "80%")));
        harness.Notifications.Shown.Clear();
        harness.Sound.Reset();
        _clock.Advance(TimeSpan.FromMinutes(1));
        harness.Apply(Ok(Crit("web01", "CPU", "99%")));

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
        Assert.Equal(Severity.Critical, Assert.Single(harness.Alerts.GetOpenIncidents()).Severity);
    }

    [Fact]
    public void Mute_persists_in_preferences_json()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-pref-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "preferences.json");
        try
        {
            var first = new JsonUserPreferencesStore(path);
            Assert.False(first.MuteSound);
            first.SetMuteSound(true);
            Assert.True(File.Exists(path));
            Assert.DoesNotContain("Secret", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Authorization", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);

            var second = new JsonUserPreferencesStore(path);
            Assert.True(second.MuteSound);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void First_ever_baseline_does_not_notify_existing_production_problems()
    {
        var harness = CreateHarness();
        var problems = Enumerable.Range(1, 120)
            .Select(i => Warn("host", $"svc{i}", "existing"))
            .ToArray();
        harness.Apply(Ok(problems));

        Assert.Empty(harness.Notifications.Shown);
        Assert.Equal(0, harness.Sound.PlayCount);
        Assert.Equal(120, harness.Alerts.GetOpenIncidents().Count);
        Assert.All(harness.Alerts.GetOpenIncidents(), incident => Assert.False(incident.IsSeen));
    }

    [Fact]
    public void Persisted_state_startup_does_not_replay_existing_notifications()
    {
        var store = new InMemoryAlertStateStore();
        var first = CreateHarness(store);
        first.BaselineEmpty();
        first.Apply(Ok(Crit("web01", "CPU", "99%"), Warn("web01", "Disk", "80%")));
        Assert.Equal(2, first.Notifications.Shown.Count);

        var restarted = CreateHarness(store);
        restarted.Apply(Ok(Crit("web01", "CPU", "99%"), Warn("web01", "Disk", "80%")));

        Assert.Empty(restarted.Notifications.Shown);
        Assert.Equal(0, restarted.Sound.PlayCount);
        Assert.Equal(2, restarted.Alerts.GetOpenIncidents().Count);
    }

    [Fact]
    public async Task Notification_backend_failure_does_not_crash_poller()
    {
        var notifications = new ThrowingNotificationService();
        var sound = new RecordingAlertSoundService();
        var preferences = new InMemoryUserPreferences();
        var coordinator = new NotificationCoordinator(notifications, sound, preferences);
        var (poller, _, client) = PollerTestHost.Create(notifications: coordinator);
        client.Snapshot = Ok();
        await poller.RefreshAsync();
        client.Snapshot = Ok(Crit("web01", "CPU", "99%"));

        var exception = await Record.ExceptionAsync(() => poller.RefreshAsync());
        Assert.Null(exception);
        Assert.Equal(ConnectionStatusKind.Connected, poller.Status.Kind);
        Assert.Equal(1, sound.PlayCount);
    }

    [Fact]
    public async Task Sound_backend_failure_does_not_crash_poller()
    {
        var notifications = new RecordingNotificationService();
        var sound = new ThrowingAlertSoundService();
        var coordinator = new NotificationCoordinator(notifications, sound, new InMemoryUserPreferences());
        var (poller, _, client) = PollerTestHost.Create(notifications: coordinator);
        client.Snapshot = Ok();
        await poller.RefreshAsync();
        client.Snapshot = Ok(Warn("web01", "CPU", "80%"));

        var exception = await Record.ExceptionAsync(() => poller.RefreshAsync());
        Assert.Null(exception);
        Assert.Single(notifications.Shown);
        Assert.Equal(ConnectionStatusKind.Connected, poller.Status.Kind);
    }

    [Fact]
    public void Sound_preview_does_not_alter_incident_state()
    {
        var harness = CreateHarness();
        harness.BaselineEmpty();
        harness.Apply(Ok(Warn("web01", "CPU", "80%")));
        var before = Assert.Single(harness.Alerts.GetOpenIncidents());
        harness.Sound.Reset();

        AlertSoundPreview.Play(harness.Sound);

        var after = Assert.Single(harness.Alerts.GetOpenIncidents());
        Assert.Equal(1, harness.Sound.PlayCount);
        Assert.Single(harness.Notifications.Shown);
        Assert.Equal(before.ObjectId, after.ObjectId);
        Assert.Equal(before.IsSeen, after.IsSeen);
        Assert.Equal(before.Severity, after.Severity);
        Assert.Equal(before.OpenedAtUtc, after.OpenedAtUtc);
    }

    [Fact]
    public void Sound_preview_bypasses_mute()
    {
        var harness = CreateHarness();
        harness.Preferences.SetMuteSound(true);
        harness.Sound.Reset();
        AlertSoundPreview.Play(harness.Sound);
        Assert.Equal(1, harness.Sound.PlayCount);
        Assert.True(harness.Preferences.MuteSound);
    }

    [Fact]
    public void Gear_and_tray_commands_use_the_same_mute_service()
    {
        var preferences = new InMemoryUserPreferences();
        MuteCommands.Toggle(preferences);
        Assert.True(preferences.MuteSound);
        Assert.Equal("Unmute sound", MuteCommands.MenuHeader(preferences, "Mute sound", "Unmute sound"));
        MuteCommands.Toggle(preferences);
        Assert.False(preferences.MuteSound);
        Assert.Equal("Mute sound", MuteCommands.MenuHeader(preferences, "Mute sound", "Unmute sound"));
    }

    [Fact]
    public async Task Coordinator_exception_does_not_crash_poller()
    {
        var (poller, _, client) = PollerTestHost.Create(notifications: new ThrowingNotificationCoordinator());
        client.Snapshot = Ok(Crit("web01", "CPU", "99%"));
        var exception = await Record.ExceptionAsync(() => poller.RefreshAsync());
        Assert.Null(exception);
        Assert.Equal(ConnectionStatusKind.Connected, poller.Status.Kind);
    }

    private Harness CreateHarness(IAlertStateStore? store = null) => new(_clock, store);

    private ProblemSnapshot Ok(params MonitoredProblem[] problems) =>
        ProblemSnapshot.Success(_clock.UtcNow, new SiteId("itssrv"), problems);

    private ProblemSnapshot Failed() =>
        ProblemSnapshot.Failure(_clock.UtcNow, SnapshotErrorKind.Unavailable, "Checkmk unreachable");

    private static MonitoredProblem Warn(string host, string service, string output, bool acknowledged = false, int downtime = 0) =>
        Problem(host, service, Severity.Warning, output, acknowledged, downtime);

    private static MonitoredProblem Crit(string host, string service, string output, DateTimeOffset? lastOk = null) =>
        Problem(host, service, Severity.Critical, output, lastOk: lastOk);

    private static MonitoredProblem Unknown(string host, string service, string output) =>
        Problem(host, service, Severity.Unknown, output);

    private static MonitoredProblem Problem(
        string host,
        string service,
        Severity severity,
        string output,
        bool acknowledged = false,
        int downtime = 0,
        DateTimeOffset? lastOk = null) =>
        new()
        {
            Id = MonitoredObjectId.Service(new SiteId("itssrv"), host, service),
            Severity = severity,
            StateType = StateType.Hard,
            PluginOutput = output,
            LastTimeOk = lastOk ?? new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
            IsAcknowledgedInCheckmk = acknowledged,
            ScheduledDowntimeDepth = downtime
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

internal sealed class RecordingNotificationService : INotificationService
{
    public List<IncidentAlert> Shown { get; } = [];

    public void Show(IncidentAlert alert) => Shown.Add(alert);
}

internal sealed class RecordingAlertSoundService : IAlertSoundService
{
    public int PlayCount { get; private set; }

    public void Play() => PlayCount++;

    public void Reset() => PlayCount = 0;
}

internal sealed class ThrowingNotificationService : INotificationService
{
    public void Show(IncidentAlert alert) => throw new InvalidOperationException("notification failed");
}

internal sealed class ThrowingAlertSoundService : IAlertSoundService
{
    public void Play() => throw new InvalidOperationException("sound failed");
}

internal sealed class ThrowingNotificationCoordinator : INotificationCoordinator
{
    public void Process(ProblemSnapshot snapshot, AlertDelta delta, bool wasVirginLocalState) =>
        throw new InvalidOperationException("coordinator failed");
}

internal sealed class MutableClock : TimeProvider
{
    public MutableClock(DateTimeOffset utcNow) => UtcNow = utcNow;

    public DateTimeOffset UtcNow { get; set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan delta) => UtcNow += delta;
}
