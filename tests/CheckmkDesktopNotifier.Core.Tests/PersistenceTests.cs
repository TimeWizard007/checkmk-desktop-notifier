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
            Assert.Equal(clock.UtcNow, reloaded.LastSuccessfulPollUtc);
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
    public void Host_recurrence_marker_survives_json_reload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-desktop-notifier-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "alert-state.json");
        var clock = new MutableTimeProvider(ProblemFactory.T0);
        var lastTimeUp = ProblemFactory.T0.AddHours(-2);

        try
        {
            var sut = new AlertStateService(new JsonAlertStateStore(path), clock);
            sut.ApplySnapshot(ProblemFactory.Ok(
                clock.UtcNow,
                ProblemFactory.Host("web01", Severity.Critical, lastTimeUp: lastTimeUp)));
            sut.MarkSeen(ProblemFactory.HostId("web01"));

            var reloaded = new AlertStateService(new JsonAlertStateStore(path), clock);
            var open = Assert.Single(reloaded.GetOpenIncidents());
            Assert.True(open.IsSeen);
            Assert.Equal(ObjectKind.Host, open.ObjectId.Kind);
            Assert.Equal(lastTimeUp, open.BoundRecurrenceMarker);
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
    public void ReplaceStore_loads_new_file_without_writing_previous_incidents()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-desktop-notifier-tests", Guid.NewGuid().ToString("N"));
        var firstPath = Path.Combine(directory, "first.json");
        var secondPath = Path.Combine(directory, "second.json");
        var clock = new MutableTimeProvider(ProblemFactory.T0);

        try
        {
            var first = new AlertStateService(new JsonAlertStateStore(firstPath), clock);
            first.ApplySnapshot(ProblemFactory.Ok(
                clock.UtcNow,
                ProblemFactory.Service("web01", "CPU", Severity.Critical, lastTimeOk: ProblemFactory.T0.AddHours(-1))));
            first.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));

            var secondStore = new JsonAlertStateStore(secondPath);
            first.ReplaceStore(secondStore);
            Assert.Empty(first.GetOpenIncidents());

            var firstReloaded = new AlertStateService(new JsonAlertStateStore(firstPath), clock);
            Assert.True(Assert.Single(firstReloaded.GetOpenIncidents()).IsSeen);
            Assert.False(File.Exists(secondPath));
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
    public void Isolated_store_reads_legacy_fallback_until_isolated_file_exists()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-desktop-notifier-tests", Guid.NewGuid().ToString("N"));
        var isolatedPath = Path.Combine(directory, "state", "connection", "alert-state.json");
        var legacyPath = Path.Combine(directory, "alert-state.json");
        var clock = new MutableTimeProvider(ProblemFactory.T0);

        try
        {
            Directory.CreateDirectory(directory);
            var legacy = new AlertStateService(new JsonAlertStateStore(legacyPath), clock);
            legacy.ApplySnapshot(ProblemFactory.Ok(
                clock.UtcNow,
                ProblemFactory.Service("web01", "CPU", Severity.Critical, lastTimeOk: ProblemFactory.T0.AddHours(-1))));
            legacy.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));
            Assert.True(File.Exists(legacyPath));
            Assert.False(File.Exists(isolatedPath));

            var isolated = new AlertStateService(new JsonAlertStateStore(isolatedPath, legacyPath), clock);
            var fromFallback = Assert.Single(isolated.GetOpenIncidents());
            Assert.True(fromFallback.IsSeen);
            Assert.Equal(ProblemFactory.ServiceId("web01", "CPU"), fromFallback.ObjectId);

            clock.UtcNow = ProblemFactory.T0.AddMinutes(5);
            isolated.ApplySnapshot(ProblemFactory.Ok(
                clock.UtcNow,
                ProblemFactory.Service("web01", "CPU", Severity.Critical, lastTimeOk: ProblemFactory.T0.AddHours(-1))));
            Assert.True(File.Exists(isolatedPath));
            Assert.True(File.Exists(legacyPath));

            File.WriteAllText(legacyPath, "{\"schemaVersion\":1,\"incidents\":[]}");
            var afterIsolatedExists = new AlertStateService(new JsonAlertStateStore(isolatedPath, legacyPath), clock);
            Assert.Single(afterIsolatedExists.GetOpenIncidents());
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
    public void Isolated_store_never_writes_to_the_legacy_fallback_path()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-desktop-notifier-tests", Guid.NewGuid().ToString("N"));
        var isolatedPath = Path.Combine(directory, "state", "connection", "alert-state.json");
        var legacyPath = Path.Combine(directory, "alert-state.json");
        var clock = new MutableTimeProvider(ProblemFactory.T0);

        try
        {
            var store = new JsonAlertStateStore(isolatedPath, legacyPath);
            var sut = new AlertStateService(store, clock);
            sut.ApplySnapshot(ProblemFactory.Ok(
                clock.UtcNow,
                ProblemFactory.Host("web01", Severity.Critical, lastTimeUp: ProblemFactory.T0.AddHours(-2))));

            Assert.True(File.Exists(isolatedPath));
            Assert.False(File.Exists(legacyPath));
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
