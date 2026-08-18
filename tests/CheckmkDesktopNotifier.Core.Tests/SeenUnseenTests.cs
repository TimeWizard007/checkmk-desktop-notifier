using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class SeenUnseenTests
{
    private readonly MutableTimeProvider _clock = new(ProblemFactory.T0);

    private AlertStateService CreateSut() => new(new InMemoryAlertStateStore(), _clock);

    [Fact]
    public void New_then_seen_then_unseen_then_seen()
    {
        var sut = CreateSut();
        var id = ProblemFactory.ServiceId("web01", "CPU");
        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, ProblemFactory.Service("web01", "CPU", Severity.Critical)));

        Assert.False(Assert.Single(sut.GetOpenIncidents()).IsSeen);

        sut.MarkSeen(id);
        Assert.True(Assert.Single(sut.GetOpenIncidents()).IsSeen);

        sut.MarkUnseen(id);
        var unseen = Assert.Single(sut.GetOpenIncidents());
        Assert.False(unseen.IsSeen);
        Assert.Equal(IncidentStatus.New, unseen.Status);

        sut.MarkSeen(id);
        Assert.True(Assert.Single(sut.GetOpenIncidents()).IsSeen);
    }

    [Fact]
    public void Unseen_increments_new_count_immediately_without_poll()
    {
        var sut = CreateSut();
        var id = ProblemFactory.ServiceId("web01", "CPU");
        sut.ApplySnapshot(ProblemFactory.Ok(
            _clock.UtcNow,
            ProblemFactory.Service("web01", "CPU", Severity.Critical),
            ProblemFactory.Service("web01", "Disk", Severity.Warning)));
        sut.MarkSeen(id);
        Assert.Equal(1, sut.GetOpenIncidents().Count(incident => !incident.IsSeen));

        sut.MarkUnseen(id);

        Assert.Equal(2, sut.GetOpenIncidents().Count(incident => !incident.IsSeen));
    }

    [Fact]
    public void Seen_decrements_new_count_immediately()
    {
        var sut = CreateSut();
        var id = ProblemFactory.ServiceId("web01", "CPU");
        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, ProblemFactory.Service("web01", "CPU", Severity.Critical)));
        Assert.Equal(1, sut.GetOpenIncidents().Count(incident => !incident.IsSeen));

        sut.MarkSeen(id);

        Assert.Equal(0, sut.GetOpenIncidents().Count(incident => !incident.IsSeen));
    }

    [Fact]
    public void Unseen_returns_incident_to_new_filter_immediately()
    {
        var sut = CreateSut();
        var id = ProblemFactory.ServiceId("web01", "CPU");
        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, ProblemFactory.Service("web01", "CPU", Severity.Critical)));
        sut.MarkSeen(id);
        Assert.Empty(ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.New));

        sut.MarkUnseen(id);

        var newest = ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.New);
        Assert.Contains(newest, incident => incident.ObjectId.Equals(id) && !incident.IsSeen);
    }

    [Fact]
    public void Unseen_does_not_modify_severity_ack_taken_or_downtime()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service(
            "web01",
            "CPU",
            Severity.Warning,
            acknowledged: true,
            downtimeDepth: 2,
            acknowledgementType: AcknowledgementType.Sticky,
            takenBy: "Michał",
            takenByNotifier: true);
        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        sut.MarkSeen(problem.Id);

        sut.MarkUnseen(problem.Id);

        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.False(open.IsSeen);
        Assert.Equal(Severity.Warning, open.Severity);
        Assert.True(open.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.Sticky, open.AcknowledgementType);
        Assert.Equal("Michał", open.TakenByDisplayName);
        Assert.True(open.IsTakenByNotifier);
        Assert.Equal(2, open.ScheduledDowntimeDepth);
    }

    [Fact]
    public void Taken_plus_unseen_remains_taken()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service(
            "GO-S11",
            "Update",
            Severity.Critical,
            acknowledged: true,
            acknowledgementType: AcknowledgementType.Sticky,
            takenBy: "Michał",
            takenByNotifier: true);
        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        sut.MarkSeen(problem.Id);
        var takenBefore = ProblemListFilterLogic.CountTaken(sut.GetOpenIncidents());

        sut.MarkUnseen(problem.Id);

        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.False(open.IsSeen);
        Assert.True(open.IsTakenByNotifier);
        Assert.Equal("Michał", open.TakenByDisplayName);
        Assert.Equal(takenBefore, ProblemListFilterLogic.CountTaken(sut.GetOpenIncidents()));
        Assert.Contains(
            ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.Taken),
            incident => incident.ObjectId.Equals(problem.Id));
        Assert.Contains(
            ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.New),
            incident => incident.ObjectId.Equals(problem.Id));
    }

    [Fact]
    public void Generic_ack_plus_unseen_remains_generic_ack()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service(
            "web01",
            "CPU",
            Severity.Critical,
            acknowledged: true,
            acknowledgementType: AcknowledgementType.Sticky);
        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        sut.MarkSeen(problem.Id);

        sut.MarkUnseen(problem.Id);

        var open = Assert.Single(sut.GetOpenIncidents());
        Assert.False(open.IsSeen);
        Assert.True(open.IsAcknowledgedInCheckmk);
        Assert.False(open.IsTakenByNotifier);
        Assert.Null(open.TakenByDisplayName);
        Assert.Empty(ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.Taken));
    }

    [Fact]
    public void Unseen_snapshot_does_not_open_a_new_incident()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service("web01", "CPU", Severity.Critical);
        var first = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        sut.MarkSeen(problem.Id);
        sut.MarkUnseen(problem.Id);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);

        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));

        Assert.Single(first.Opened);
        Assert.Empty(delta.Opened);
        Assert.Empty(delta.Recovered);
        Assert.False(Assert.Single(sut.GetOpenIncidents()).IsSeen);
    }

    [Fact]
    public void Repeated_seen_unseen_does_not_open_incidents()
    {
        var sut = CreateSut();
        var problem = ProblemFactory.Service("web01", "CPU", Severity.Critical);
        sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        for (var i = 0; i < 5; i++)
        {
            sut.MarkSeen(problem.Id);
            sut.MarkUnseen(problem.Id);
            sut.MarkSeen(problem.Id);
        }

        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        var delta = sut.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
        Assert.Empty(delta.Opened);
        Assert.True(Assert.Single(sut.GetOpenIncidents()).IsSeen);
    }

    [Fact]
    public void Mark_all_new_as_seen_still_works()
    {
        var sut = CreateSut();
        sut.ApplySnapshot(ProblemFactory.Ok(
            _clock.UtcNow,
            ProblemFactory.Service("web01", "CPU", Severity.Critical),
            ProblemFactory.Service("web01", "Disk", Severity.Warning)));
        sut.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));
        sut.MarkUnseen(ProblemFactory.ServiceId("web01", "CPU"));

        sut.MarkAllNewAsSeen();

        Assert.All(sut.GetOpenIncidents(), incident => Assert.True(incident.IsSeen));
        Assert.Empty(ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.New));
    }

    [Fact]
    public void Unseen_search_and_filter_composition_remains_correct()
    {
        var sut = CreateSut();
        var taken = ProblemFactory.Service(
            "GO-S11",
            "Update",
            Severity.Critical,
            acknowledged: true,
            acknowledgementType: AcknowledgementType.Sticky,
            takenBy: "Michał",
            takenByNotifier: true);
        sut.ApplySnapshot(ProblemFactory.Ok(
            _clock.UtcNow,
            taken,
            ProblemFactory.Service("web01", "CPU", Severity.Warning)));
        sut.MarkSeen(taken.Id);

        sut.MarkUnseen(taken.Id);

        var newest = ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.New, "Update");
        Assert.Single(newest);
        var takenSearch = ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.Taken, "Michał");
        Assert.False(Assert.Single(takenSearch).IsSeen);
        var critHost = ProblemListFilterLogic.Apply(sut.GetOpenIncidents(), ProblemListFilter.Critical, "GO-S11");
        Assert.Single(critHost);
    }

    [Fact]
    public void Unseen_persists_across_restart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-desktop-notifier-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "alert-state.json");
        try
        {
            var store = new JsonAlertStateStore(path);
            var first = new AlertStateService(store, _clock);
            var problem = ProblemFactory.Service("web01", "CPU", Severity.Critical);
            first.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, problem));
            first.MarkSeen(problem.Id);
            first.MarkUnseen(problem.Id);

            var restarted = new AlertStateService(new JsonAlertStateStore(path), _clock);
            var open = Assert.Single(restarted.GetOpenIncidents());
            Assert.False(open.IsSeen);
            Assert.Equal(IncidentStatus.New, open.Status);
            Assert.Equal(Severity.Critical, open.Severity);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
