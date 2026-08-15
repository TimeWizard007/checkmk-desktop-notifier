using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Mock;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class AlertStateEngineTests
{
    private readonly MutableTimeProvider _clock = new(ProblemFactory.T0);
    private readonly InMemoryAlertStateStore _store = new();

    private AlertStateService CreateSut() => new(_store, _clock);

    [Fact]
    public void Crit_then_Crit_is_one_incident()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service("web01", "CPU", Severity.Critical);

        var first = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var second = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));

        Assert.Single(first.Opened);
        Assert.Empty(second.Opened);
        Assert.Empty(second.Recovered);
        Assert.Single(sut.GetOpenIncidents());
        Assert.False(sut.GetOpenIncidents()[0].IsSeen);
        Assert.Equal(IncidentStatus.New, sut.GetOpenIncidents()[0].Status);
    }

    [Fact]
    public void Crit_Seen_then_Crit_stays_Seen()
    {
        var sut = CreateSut();
        var id = ProblemFactory.ServiceId("web01", "CPU");
        var problem = ProblemFactory.Service("web01", "CPU", Severity.Critical);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        sut.MarkSeen(id);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));

        Assert.Empty(delta.Opened);
        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.True(open.IsSeen);
        Assert.Equal(IncidentStatus.Seen, open.Status);
    }

    [Fact]
    public void Crit_then_Ok_is_Recovered()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service("web01", "CPU", Severity.Critical);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow));

        Assert.Empty(delta.Opened);
        var recovered = Assert.Single(delta.Recovered);
        Assert.Equal(ProblemFactory.ServiceId("web01", "CPU"), recovered.ObjectId);
        Assert.Empty(sut.GetOpenIncidents());
    }

    [Fact]
    public void Crit_Ok_Crit_is_new_incident()
    {
        var sut = CreateSut();
        var id = ProblemFactory.ServiceId("web01", "CPU");
        var problem = ProblemFactory.Service("web01", "CPU", Severity.Critical);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        sut.MarkSeen(id);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow));
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));

        var opened = Assert.Single(delta.Opened);
        Assert.False(opened.IsSeen);
        Assert.Equal(IncidentStatus.New, opened.Status);
        Assert.Single(sut.GetOpenIncidents());
    }

    [Fact]
    public void Warn_then_Crit_is_same_uninterrupted_incident()
    {
        var sut = CreateSut();
        var lastTimeOk = ProblemFactory.T0.AddHours(-2);
        var warn = ProblemFactory.Service("web01", "Disk", Severity.Warning, lastTimeOk: lastTimeOk);
        var crit = ProblemFactory.Service("web01", "Disk", Severity.Critical, lastTimeOk: lastTimeOk);

        var first = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, warn));
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var second = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, crit));

        Assert.Single(first.Opened);
        Assert.Empty(second.Opened);
        Assert.Empty(second.Recovered);
        var changed = Assert.Single(second.SeverityChanged);
        Assert.Equal(Severity.Critical, changed.Severity);
        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.Equal(first.Opened[0].OpenedAtUtc, open.OpenedAtUtc);
        Assert.Equal(Severity.Critical, open.Severity);
        Assert.False(open.IsSeen);
    }

    [Fact]
    public void Seen_Warn_then_Crit_stays_Seen()
    {
        var sut = CreateSut();
        var id = ProblemFactory.ServiceId("web01", "Disk");
        var lastTimeOk = ProblemFactory.T0.AddHours(-2);
        var warn = ProblemFactory.Service("web01", "Disk", Severity.Warning, lastTimeOk: lastTimeOk);
        var crit = ProblemFactory.Service("web01", "Disk", Severity.Critical, lastTimeOk: lastTimeOk);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, warn));
        sut.MarkSeen(id);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, crit));

        Assert.Empty(delta.Opened);
        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.True(open.IsSeen);
        Assert.Equal(Severity.Critical, open.Severity);
    }

    [Fact]
    public void Failed_snapshot_does_not_mark_anything_Recovered()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service("web01", "CPU", Severity.Critical);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        sut.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var delta = sut.ApplySnapshot(ProblemFactory.Failed(_clock.UtcNow));

        Assert.True(delta.IsEmpty);
        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.True(open.IsSeen);
        Assert.Equal(Severity.Critical, open.Severity);
    }

    [Fact]
    public void Host_and_service_identities_do_not_collide()
    {
        var sut = CreateSut();
        var host = ProblemFactory.Host("web01", Severity.Critical);
        var service = ProblemFactory.Service("web01", "CPU", Severity.Critical);

        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, host, service));

        Assert.Equal(2, delta.Opened.Count);
        Assert.Equal(2, sut.GetOpenIncidents().Count);
        Assert.Contains(sut.GetOpenIncidents(), i => i.ObjectId.Kind == ObjectKind.Host);
        Assert.Contains(sut.GetOpenIncidents(), i => i.ObjectId.Kind == ObjectKind.Service);
        Assert.NotEqual(ProblemFactory.HostId("web01"), ProblemFactory.ServiceId("web01", "CPU"));
    }

    [Fact]
    public void Recurrence_detected_via_last_time_ok()
    {
        var sut = CreateSut();
        var firstOk = ProblemFactory.T0.AddHours(-3);
        var laterOk = ProblemFactory.T0.AddMinutes(-5);
        var first = ProblemFactory.Service("web01", "CPU", Severity.Critical, lastTimeOk: firstOk);
        var afterGap = ProblemFactory.Service("web01", "CPU", Severity.Critical, lastTimeOk: laterOk);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, first));
        sut.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, afterGap));

        Assert.Single(delta.Recovered);
        var opened = Assert.Single(delta.Opened);
        Assert.False(opened.IsSeen);
        Assert.Equal(laterOk, opened.BoundRecurrenceMarker);
        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.Equal(IncidentStatus.New, open.Status);
    }

    [Fact]
    public void Recurrence_detected_via_last_time_up()
    {
        var sut = CreateSut();
        var firstUp = ProblemFactory.T0.AddHours(-4);
        var laterUp = ProblemFactory.T0.AddMinutes(-2);
        var first = ProblemFactory.Host("web01", Severity.Critical, lastTimeUp: firstUp);
        var afterGap = ProblemFactory.Host("web01", Severity.Critical, lastTimeUp: laterUp);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, first));
        sut.MarkSeen(ProblemFactory.HostId("web01"));
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, afterGap));

        Assert.Single(delta.Recovered);
        var opened = Assert.Single(delta.Opened);
        Assert.False(opened.IsSeen);
        Assert.Equal(laterUp, opened.BoundRecurrenceMarker);
        Assert.Equal(ObjectKind.Host, opened.ObjectId.Kind);
    }

    [Fact]
    public void Multiple_new_incidents_in_one_snapshot()
    {
        var sut = CreateSut();
        var cpu = ProblemFactory.Service("web01", "CPU", Severity.Critical);
        var disk = ProblemFactory.Service("web01", "Disk", Severity.Warning);
        var host = ProblemFactory.Host("db01", Severity.Unknown);

        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, cpu, disk, host));

        Assert.Equal(3, delta.Opened.Count);
        Assert.All(delta.Opened, incident => Assert.False(incident.IsSeen));
        Assert.Equal(3, sut.GetOpenIncidents().Count);
    }

    [Fact]
    public void Mark_all_new_as_seen()
    {
        var sut = CreateSut();
        var cpu = ProblemFactory.Service("web01", "CPU", Severity.Critical);
        var disk = ProblemFactory.Service("web01", "Disk", Severity.Warning);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, cpu, disk));
        sut.MarkAllNewAsSeen();

        Assert.Equal(2, sut.GetOpenIncidents().Count);
        Assert.All(sut.GetOpenIncidents(), incident => Assert.True(incident.IsSeen));
    }

    [Fact]
    public async Task Mock_client_feeds_engine_without_http()
    {
        var client = new MockCheckmkClient
        {
            NextSnapshot = ProblemFactory.Ok(
                _clock.UtcNow,
                ProblemFactory.Service("web01", "CPU", Severity.Critical))
        };
        var sut = CreateSut();

        var snapshot = await client.GetCurrentProblemsAsync();
        var delta = sut.ApplySnapshot(snapshot);

        Assert.Single(delta.Opened);
    }

    [Fact]
    public void Soft_state_does_not_open_an_incident()
    {
        var sut = CreateSut();
        var soft = ProblemFactory.Service("web01", "CPU", Severity.Critical, StateType.Soft);

        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, soft));

        Assert.Empty(delta.Opened);
        Assert.Empty(sut.GetOpenIncidents());
    }

    [Fact]
    public void Checkmk_acknowledgement_does_not_set_local_Seen()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service(
            "web01",
            "CPU",
            Severity.Critical,
            acknowledged: true);

        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));

        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.True(open.IsAcknowledgedInCheckmk);
        Assert.False(open.IsSeen);
    }
}
