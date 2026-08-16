using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class ConnectionIdentityAndCoordinatorTests
{
    [Fact]
    public void Different_base_urls_with_same_site_have_different_identities()
    {
        var a = ConnectionIdentity.From("https://checkmk-a.example.invalid", "mysite");
        var b = ConnectionIdentity.From("https://checkmk-b.example.invalid", "mysite");
        Assert.False(a.EqualsIdentity(b));
        Assert.NotEqual(a.FileId, b.FileId);
    }

    [Fact]
    public async Task Apply_uses_isolated_alert_state_per_identity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-coordinator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var paths = new AppStoragePaths(directory);
            var clock = TimeProvider.System;
            var alerts = new AlertStateService(new InMemoryAlertStateStore(), clock);
            var inner = new DelegatingCheckmkClient(new UnconfiguredCheckmkClient());
            var poller = new CheckmkPoller(
                inner,
                alerts,
                new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 60 },
                clock);
            var handler = new RecordingHandler { Responder = Ok };
            var coordinator = new MonitoringCoordinator(inner, alerts, poller, paths, clock, handler);

            var first = TestOptions.Real();
            await coordinator.ApplyAsync(first);
            alerts.ApplySnapshot(ProblemSnapshot.Success(
                clock.GetUtcNow(),
                new SiteId("mysite"),
                [
                    new MonitoredProblem
                    {
                        Id = MonitoredObjectId.Service(new SiteId("mysite"), "web01", "CPU"),
                        Severity = Severity.Critical,
                        StateType = StateType.Hard
                    }
                ]));
            Assert.Single(alerts.GetOpenIncidents());

            var second = new CheckmkOptions
            {
                Mode = ClientMode.Real,
                BaseUrl = "https://other.example.invalid",
                Site = "mysite",
                Username = "automation",
                Secret = TestOptions.Secret,
                PollIntervalSeconds = 60
            };
            await coordinator.ApplyAsync(second);
            Assert.Empty(alerts.GetOpenIncidents());

            var firstPath = paths.AlertStatePathFor(ConnectionIdentity.From(first.BaseUrl!, first.Site!));
            Assert.True(File.Exists(firstPath));
            var restored = new AlertStateService(new JsonAlertStateStore(firstPath), clock);
            Assert.Single(restored.GetOpenIncidents());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Changing_poll_interval_updates_poller()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-coordinator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var paths = new AppStoragePaths(directory);
            var clock = TimeProvider.System;
            var alerts = new AlertStateService(new InMemoryAlertStateStore(), clock);
            var inner = new DelegatingCheckmkClient(new UnconfiguredCheckmkClient());
            var poller = new CheckmkPoller(
                inner,
                alerts,
                new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 60 },
                clock);
            var coordinator = new MonitoringCoordinator(
                inner,
                alerts,
                poller,
                paths,
                clock,
                new RecordingHandler { Responder = Ok });

            var options = TestOptions.Real();
            await coordinator.ApplyAsync(options);
            Assert.Equal(TimeSpan.FromSeconds(60), poller.Interval);

            await coordinator.ApplyAsync(new CheckmkOptions
            {
                Mode = ClientMode.Real,
                BaseUrl = options.BaseUrl,
                Site = options.Site,
                Username = options.Username,
                Secret = options.Secret,
                PollIntervalSeconds = 15
            });
            Assert.Equal(TimeSpan.FromSeconds(15), poller.Interval);
            Assert.True(coordinator.IsPollingEnabled);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Reset_stops_polling_without_deleting_alert_state()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-coordinator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var paths = new AppStoragePaths(directory);
            var clock = TimeProvider.System;
            var alerts = new AlertStateService(new InMemoryAlertStateStore(), clock);
            var inner = new DelegatingCheckmkClient(new UnconfiguredCheckmkClient());
            var poller = new CheckmkPoller(
                inner,
                alerts,
                new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 60 },
                clock);
            var coordinator = new MonitoringCoordinator(
                inner,
                alerts,
                poller,
                paths,
                clock,
                new RecordingHandler { Responder = Ok });

            await coordinator.ApplyAsync(TestOptions.Real());
            alerts.ApplySnapshot(ProblemSnapshot.Success(
                clock.GetUtcNow(),
                new SiteId("mysite"),
                [
                    new MonitoredProblem
                    {
                        Id = MonitoredObjectId.Host(new SiteId("mysite"), "web01"),
                        Severity = Severity.Critical,
                        StateType = StateType.Hard
                    }
                ]));

            await coordinator.ResetPollingAsync();
            Assert.False(coordinator.IsPollingEnabled);
            Assert.Single(alerts.GetOpenIncidents());
            Assert.IsType<UnconfiguredCheckmkClient>(inner.Inner);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Alert_state_json_does_not_contain_secret()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-coordinator-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "alert-state.json");
        Directory.CreateDirectory(directory);
        try
        {
            var clock = TimeProvider.System;
            var store = new JsonAlertStateStore(path);
            var alerts = new AlertStateService(store, clock);
            alerts.ApplySnapshot(ProblemSnapshot.Success(
                clock.GetUtcNow(),
                new SiteId("mysite"),
                [
                    new MonitoredProblem
                    {
                        Id = MonitoredObjectId.Service(new SiteId("mysite"), "web01", "CPU"),
                        Severity = Severity.Critical,
                        StateType = StateType.Hard,
                        PluginOutput = "CPU is high"
                    }
                ]));

            var json = File.ReadAllText(path);
            Assert.DoesNotContain(TestOptions.Secret, json, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Bearer", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static HttpResponseMessage Ok(HttpRequestMessage _) =>
        new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"value\":[]}", System.Text.Encoding.UTF8, "application/json")
        };
}
