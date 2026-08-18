using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Navigation;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class CheckmkProblemNavigatorTests
{
    [Fact]
    public void Successful_open_launches_gui_uri_and_does_not_change_incident_state()
    {
        var clock = new MutableTimeProvider(ProblemFactory.T0);
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), clock);
        var problem = ProblemFactory.Service(
            "web01",
            "CPU",
            Severity.Critical,
            acknowledged: true,
            downtimeDepth: 1,
            acknowledgementType: AcknowledgementType.Sticky,
            takenBy: "Michał",
            takenByNotifier: true);
        alerts.ApplySnapshot(ProblemFactory.Ok(clock.UtcNow, problem));
        alerts.MarkSeen(problem.Id);
        var before = Assert.Single(alerts.GetOpenIncidents());
        Uri? launched = null;
        var navigator = new CheckmkProblemNavigator(
            () => ("https://checkmk.example.invalid", "mysite"),
            uri => launched = uri);

        var result = navigator.Open(problem.Id);

        Assert.True(result.Opened);
        Assert.NotNull(launched);
        Assert.Same(launched, result.Target);
        Assert.Contains("/mysite/check_mk/index.py", launched!.AbsoluteUri, StringComparison.Ordinal);
        var after = Assert.Single(alerts.GetOpenIncidents());
        Assert.Equal(before.IsSeen, after.IsSeen);
        Assert.Equal(before.Severity, after.Severity);
        Assert.Equal(before.IsAcknowledgedInCheckmk, after.IsAcknowledgedInCheckmk);
        Assert.Equal(before.TakenByDisplayName, after.TakenByDisplayName);
        Assert.Equal(before.IsTakenByNotifier, after.IsTakenByNotifier);
        Assert.Equal(before.ScheduledDowntimeDepth, after.ScheduledDowntimeDepth);
        Assert.True(after.IsSeen);
        Assert.Equal(Severity.Critical, after.Severity);
        Assert.True(after.IsAcknowledgedInCheckmk);
        Assert.Equal("Michał", after.TakenByDisplayName);
        Assert.Equal(1, after.ScheduledDowntimeDepth);
    }

    [Fact]
    public void Missing_origin_fails_safely_without_launch()
    {
        var launched = false;
        var navigator = new CheckmkProblemNavigator(
            () => (null, null),
            _ => launched = true);

        var result = navigator.Open(ProblemFactory.HostId("web01"));

        Assert.False(result.Opened);
        Assert.Null(result.Target);
        Assert.False(launched);
    }

    [Fact]
    public void Browser_launch_failure_does_not_throw()
    {
        var navigator = new CheckmkProblemNavigator(
            () => ("https://checkmk.example.invalid", "mysite"),
            _ => throw new InvalidOperationException("browser failed"));

        var exception = Record.Exception(() => navigator.Open(ProblemFactory.ServiceId("web01", "CPU")));

        Assert.Null(exception);
        Assert.False(navigator.Open(ProblemFactory.ServiceId("web01", "CPU")).Opened);
    }

    [Fact]
    public void Origin_provider_failure_does_not_throw()
    {
        var navigator = new CheckmkProblemNavigator(
            () => throw new InvalidOperationException("origin failed"),
            _ => throw new InvalidOperationException("should not launch"));

        var exception = Record.Exception(() => navigator.Open(ProblemFactory.HostId("web01")));
        Assert.Null(exception);
        Assert.False(navigator.Open(ProblemFactory.HostId("web01")).Opened);
    }
}
