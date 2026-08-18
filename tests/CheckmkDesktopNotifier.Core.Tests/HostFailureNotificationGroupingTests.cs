using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class HostFailureNotificationGroupingTests
{
    private readonly DateTimeOffset _now = ProblemFactory.T0;

    [Fact]
    public void Host_down_plus_child_services_emits_one_grouped_alert()
    {
        var snapshot = Snapshot(
            HostDown("SRV-SQL01"),
            Service("SRV-SQL01", "CPU", Severity.Critical),
            Service("SRV-SQL01", "Memory", Severity.Unknown));
        var alerts = Select(snapshot);

        var alert = Assert.Single(alerts);
        Assert.True(alert.IsGroupedHostFailure);
        Assert.Equal(Severity.Critical, alert.Severity);
        Assert.Equal("HOST DOWN\nSRV-SQL01\n2 affected services", alert.Body);
    }

    [Fact]
    public void Child_services_are_not_selected_as_separate_alerts()
    {
        var snapshot = Snapshot(
            HostDown("SRV-SQL01"),
            Service("SRV-SQL01", "CPU", Severity.Critical),
            Service("SRV-SQL01", "Disk", Severity.Warning));
        var alerts = Select(snapshot);
        Assert.DoesNotContain(alerts, alert => alert.ObjectId.Kind == ObjectKind.Service);
    }

    [Fact]
    public void Affected_count_comes_from_snapshot_services_not_host_row()
    {
        var snapshot = Snapshot(
            HostDown("web01"),
            Service("web01", "a", Severity.Critical),
            Service("web01", "b", Severity.Warning),
            Service("web01", "c", Severity.Unknown));
        var key = HostFailureNotificationGrouping.KeyOf(ProblemFactory.HostId("web01"));
        Assert.Equal(3, HostFailureNotificationGrouping.CountAffectedServices(snapshot, key));
    }

    [Fact]
    public void Service_only_new_without_host_failure_is_not_grouped()
    {
        var snapshot = Snapshot(Service("web01", "CPU", Severity.Warning));
        var alert = Assert.Single(Select(snapshot));
        Assert.False(alert.IsGroupedHostFailure);
        Assert.Equal(ObjectKind.Service, alert.ObjectId.Kind);
    }

    [Fact]
    public void Different_hostname_is_not_grouped()
    {
        var snapshot = Snapshot(
            HostDown("down-host"),
            Service("down-host", "CPU", Severity.Critical),
            Service("other-host", "CPU", Severity.Warning));
        var alerts = Select(snapshot);
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, alert => alert.IsGroupedHostFailure && alert.ObjectId.HostName == "down-host");
        Assert.Contains(alerts, alert => !alert.IsGroupedHostFailure && alert.ObjectId.HostName == "other-host");
    }

    [Fact]
    public void Different_site_is_not_grouped()
    {
        var otherSite = new SiteId("othersite");
        var snapshot = ProblemSnapshot.Success(_now, ProblemFactory.DefaultSite,
        [
            HostDown("shared"),
            Service("shared", "local", Severity.Critical),
            new()
            {
                Id = MonitoredObjectId.Service(otherSite, "shared", "remote"),
                Severity = Severity.Critical,
                StateType = StateType.Hard,
                LastTimeOk = _now.AddHours(-1)
            }
        ]);

        var alerts = Select(snapshot);
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, alert => alert.IsGroupedHostFailure && alert.ObjectId.SiteId.Equals(ProblemFactory.DefaultSite));
        Assert.Contains(alerts, alert => !alert.IsGroupedHostFailure && alert.ObjectId.SiteId.Equals(otherSite));
    }

    [Fact]
    public void Failed_snapshot_selects_no_alerts()
    {
        var opened = new OpenIncident
        {
            ObjectId = ProblemFactory.HostId("web01"),
            Severity = Severity.Critical,
            IsSeen = false,
            OpenedAtUtc = _now,
            LastObservedAtUtc = _now
        };
        var delta = new AlertDelta([opened], [], []);
        Assert.Empty(HostFailureNotificationGrouping.SelectAlerts(ProblemFactory.Failed(_now), delta));
    }

    [Fact]
    public void Soft_host_is_not_a_grouping_host()
    {
        var snapshot = Snapshot(
            ProblemFactory.Host("web01", Severity.Critical, StateType.Soft),
            Service("web01", "CPU", Severity.Critical));
        var alert = Assert.Single(Select(snapshot));
        Assert.False(alert.IsGroupedHostFailure);
        Assert.Equal(ObjectKind.Service, alert.ObjectId.Kind);
    }

    [Fact]
    public void Unreachable_host_groups_as_unknown()
    {
        var snapshot = Snapshot(
            HostUnreachable("edge01"),
            Service("edge01", "Agent", Severity.Unknown));
        var alert = Assert.Single(Select(snapshot));
        Assert.True(alert.IsGroupedHostFailure);
        Assert.Equal(Severity.Unknown, alert.Severity);
        Assert.StartsWith("HOST UNREACHABLE\n", alert.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Acknowledged_host_does_not_emit_grouped_alert()
    {
        var snapshot = Snapshot(
            ProblemFactory.Host(
                "SRV-SQL01",
                Severity.Critical,
                pluginOutput: "DOWN",
                lastTimeUp: _now.AddHours(-2),
                acknowledged: true),
            Service("SRV-SQL01", "CPU", Severity.Critical));
        Assert.Empty(Select(snapshot));
    }

    [Fact]
    public void Child_incidents_stay_visible_when_host_is_acknowledged()
    {
        var store = new InMemoryAlertStateStore();
        var alerts = new AlertStateService(store);
        var snapshot = Snapshot(
            ProblemFactory.Host(
                "web01",
                Severity.Critical,
                pluginOutput: "DOWN",
                lastTimeUp: _now.AddHours(-2),
                acknowledged: true),
            Service("web01", "CPU", Severity.Critical),
            Service("web01", "Disk", Severity.Warning));
        alerts.ApplySnapshot(ProblemSnapshot.Success(_now.AddMinutes(-1), ProblemFactory.DefaultSite, []));
        alerts.ApplySnapshot(snapshot);
        Assert.Equal(3, alerts.GetOpenIncidents().Count);
        Assert.All(alerts.GetOpenIncidents(), incident => Assert.False(incident.IsSeen));
        Assert.Empty(Select(snapshot));
    }

    [Fact]
    public void Already_down_host_suppresses_new_children_without_a_new_host_alert()
    {
        var store = new InMemoryAlertStateStore();
        var alerts = new AlertStateService(store);
        alerts.ApplySnapshot(Snapshot(HostDown("web01")));
        var second = Snapshot(
            HostDown("web01"),
            Service("web01", "CPU", Severity.Critical),
            Service("web01", "Disk", Severity.Warning));
        var delta = alerts.ApplySnapshot(second);
        Assert.Equal(2, delta.Opened.Count);
        Assert.Empty(HostFailureNotificationGrouping.SelectAlerts(second, delta));
        Assert.Equal(3, alerts.GetOpenIncidents().Count);
        Assert.All(alerts.GetOpenIncidents(), incident => Assert.False(incident.IsSeen));
    }

    private IReadOnlyList<IncidentAlert> Select(ProblemSnapshot snapshot)
    {
        var alerts = new AlertStateService(new InMemoryAlertStateStore());
        alerts.ApplySnapshot(ProblemSnapshot.Success(_now.AddMinutes(-1), ProblemFactory.DefaultSite, []));
        var delta = alerts.ApplySnapshot(snapshot);
        return HostFailureNotificationGrouping.SelectAlerts(snapshot, delta);
    }

    private ProblemSnapshot Snapshot(params MonitoredProblem[] problems) =>
        ProblemSnapshot.Success(_now, ProblemFactory.DefaultSite, problems);

    private MonitoredProblem HostDown(string host) =>
        ProblemFactory.Host(host, Severity.Critical, pluginOutput: "DOWN", lastTimeUp: _now.AddHours(-2));

    private MonitoredProblem HostUnreachable(string host) =>
        ProblemFactory.Host(host, Severity.Unknown, pluginOutput: "UNREACHABLE", lastTimeUp: _now.AddHours(-2));

    private MonitoredProblem Service(string host, string name, Severity severity) =>
        ProblemFactory.Service(host, name, severity, lastTimeOk: _now.AddHours(-1), pluginOutput: name);
}
