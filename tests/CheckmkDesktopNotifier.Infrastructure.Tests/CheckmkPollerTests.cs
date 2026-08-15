using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkPollerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task First_refresh_runs_immediately()
    {
        var (poller, _, client) = PollerTestHost.Create();
        client.Snapshot = SuccessSnapshot(T0, ServiceProblem());

        await poller.RefreshAsync();

        Assert.Equal(1, client.Calls);
        Assert.Equal(ConnectionStatusKind.Connected, poller.Status.Kind);
    }

    [Fact]
    public async Task RunLoop_polls_immediately_then_at_configured_interval()
    {
        var clock = new FakeTimeProvider(T0);
        var options = new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 10 };
        var (poller, _, client) = PollerTestHost.Create(options, clock);
        client.Snapshot = SuccessSnapshot(T0, ServiceProblem());
        using var cts = new CancellationTokenSource();

        var loop = poller.RunLoopAsync(cts.Token);
        await WaitUntil(() => client.Calls == 1);
        Assert.Equal(T0, client.CallTimes[0]);

        await Task.Delay(30);
        clock.Advance(TimeSpan.FromSeconds(9));
        await Task.Delay(30);
        Assert.Equal(1, client.Calls);

        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntil(() => client.Calls == 2);
        Assert.Equal(T0.AddSeconds(10), client.CallTimes[1]);

        cts.Cancel();
        await loop.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Repeated_refresh_calls_client_again()
    {
        var (poller, _, client) = PollerTestHost.Create();
        client.Snapshot = SuccessSnapshot(T0, ServiceProblem());

        await poller.RefreshAsync();
        await poller.RefreshAsync();

        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public void Configured_interval_is_used()
    {
        var options = new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 30 };
        var (poller, _, _) = PollerTestHost.Create(options);

        Assert.Equal(TimeSpan.FromSeconds(30), poller.Interval);
    }

    [Fact]
    public async Task Overlapping_refresh_is_skipped()
    {
        var (poller, _, client) = PollerTestHost.Create();
        client.Gate = new TaskCompletionSource();
        client.Snapshot = SuccessSnapshot(T0, ServiceProblem());

        var first = poller.RefreshAsync();
        while (client.Calls == 0)
        {
            await Task.Delay(10);
        }

        var second = poller.RefreshAsync();
        await second.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, client.Calls);

        client.Gate.SetResult();
        await first;
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task RunLoop_stops_on_cancellation()
    {
        var options = new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 60 };
        var (poller, _, client) = PollerTestHost.Create(options);
        using var cts = new CancellationTokenSource();
        client.Handler = _ =>
        {
            cts.Cancel();
            return Task.FromResult(SuccessSnapshot(T0, ServiceProblem()));
        };

        await poller.RunLoopAsync(cts.Token);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Failed_poll_preserves_current_incidents()
    {
        var (poller, alerts, client) = PollerTestHost.Create();
        var problem = ServiceProblem();
        client.Snapshot = SuccessSnapshot(T0, problem);
        await poller.RefreshAsync();
        alerts.MarkSeen(problem.Id);

        client.Snapshot = ProblemSnapshot.Failure(T0.AddMinutes(1), SnapshotErrorKind.Unavailable, "down");
        await poller.RefreshAsync();

        var open = Assert.Single(alerts.GetOpenIncidents());
        Assert.True(open.IsSeen);
        Assert.Equal(problem.Id, open.ObjectId);
        Assert.Equal(T0, alerts.LastSuccessfulPollUtc);
        Assert.Equal(ConnectionStatusKind.Error, poller.Status.Kind);
    }

    [Fact]
    public async Task Success_after_failure_resumes_processing()
    {
        var (poller, alerts, client) = PollerTestHost.Create();
        var first = ServiceProblem("web01", "CPU");
        client.Snapshot = SuccessSnapshot(T0, first);
        await poller.RefreshAsync();

        client.Snapshot = ProblemSnapshot.Failure(T0.AddSeconds(60), SnapshotErrorKind.Unavailable, "down");
        await poller.RefreshAsync();
        Assert.Single(alerts.GetOpenIncidents());

        var second = ServiceProblem("web02", "Disk");
        client.Snapshot = SuccessSnapshot(T0.AddSeconds(120), second);
        await poller.RefreshAsync();

        var open = Assert.Single(alerts.GetOpenIncidents());
        Assert.Equal("web02", open.ObjectId.HostName);
        Assert.Equal(T0.AddSeconds(120), alerts.LastSuccessfulPollUtc);
        Assert.Equal(ConnectionStatusKind.Connected, poller.Status.Kind);
    }

    [Fact]
    public async Task Successful_poll_updates_last_successful_poll_utc()
    {
        var (poller, alerts, client) = PollerTestHost.Create();
        Assert.Null(alerts.LastSuccessfulPollUtc);

        client.Snapshot = SuccessSnapshot(T0.AddMinutes(5), ServiceProblem());
        await poller.RefreshAsync();

        Assert.Equal(T0.AddMinutes(5), alerts.LastSuccessfulPollUtc);
        Assert.Equal(T0.AddMinutes(5), poller.Status.LastSuccessfulPollUtc);
    }

    [Fact]
    public async Task Diagnostics_file_does_not_contain_secrets()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmk-poller-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "last-poll.txt");
        try
        {
            var diagnostics = new PollDiagnosticsWriter(path);
            var (poller, _, client) = PollerTestHost.Create(diagnostics: diagnostics);
            client.Snapshot = ProblemSnapshot.Failure(
                T0,
                SnapshotErrorKind.Authentication,
                $"Bearer automation {TestOptions.Secret} rejected");

            await poller.RefreshAsync();

            var text = File.ReadAllText(path);
            Assert.DoesNotContain(TestOptions.Secret, text, StringComparison.Ordinal);
            Assert.DoesNotContain("Bearer", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Success: false", text, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }

    private static ProblemSnapshot SuccessSnapshot(DateTimeOffset retrievedAt, params MonitoredProblem[] problems) =>
        ProblemSnapshot.Success(retrievedAt, new SiteId("mysite"), problems);

    private static MonitoredProblem ServiceProblem(string host = "web01", string service = "CPU") =>
        new()
        {
            Id = MonitoredObjectId.Service(new SiteId("mysite"), host, service),
            Severity = Severity.Critical,
            StateType = StateType.Hard,
            LastTimeOk = T0.AddHours(-1)
        };
}
