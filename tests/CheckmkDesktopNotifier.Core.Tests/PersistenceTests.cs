using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public void State_persistence_round_trip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-desktop-notifier-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "alert-state.json");
        var clock = new MutableTimeProvider(ProblemFactory.T0);
        var lastTimeOk = ProblemFactory.T0.AddHours(-1);

        try
        {
            var store = new JsonAlertStateStore(path);
            var sut = new AlertStateService(store, clock);
            var problem = ProblemFactory.Service(
                "web01",
                "CPU",
                Severity.Critical,
                lastTimeOk: lastTimeOk,
                pluginOutput: "CPU is 99%",
                acknowledged: true);

            sut.ApplySnapshot(ProblemFactory.Ok(clock.UtcNow, problem));
            sut.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));

            var reloaded = new AlertStateService(new JsonAlertStateStore(path), clock);
            var open = Assert.Single(reloaded.GetOpenIncidents());

            Assert.Equal(ProblemFactory.ServiceId("web01", "CPU"), open.ObjectId);
            Assert.Equal(Severity.Critical, open.Severity);
            Assert.True(open.IsSeen);
            Assert.Equal(lastTimeOk, open.BoundRecurrenceMarker);
            Assert.Equal("CPU is 99%", open.LastSummary);
            Assert.True(open.IsAcknowledgedInCheckmk);
            Assert.True(File.Exists(path));
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
    public void In_memory_store_survives_within_service_lifetime()
    {
        var store = new InMemoryAlertStateStore();
        var clock = new MutableTimeProvider(ProblemFactory.T0);
        var first = new AlertStateService(store, clock);
        first.ApplySnapshot(ProblemFactory.Ok(
            clock.UtcNow,
            ProblemFactory.Host("web01", Severity.Critical, lastTimeUp: ProblemFactory.T0.AddHours(-2))));
        first.MarkSeen(ProblemFactory.HostId("web01"));

        var second = new AlertStateService(store, clock);
        var open = Assert.Single(second.GetOpenIncidents());
        Assert.True(open.IsSeen);
        Assert.Equal(ObjectKind.Host, open.ObjectId.Kind);
    }
}
