using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class IncidentAlertFormatterTests
{
    [Fact]
    public void Critical_service_body_is_concise()
    {
        var alert = IncidentAlertFormatter.From(Create(
            ProblemFactory.ServiceId("SRV-SQL01", "CPU utilization"),
            Severity.Critical,
            "97.4%"));

        Assert.Equal("Checkmk Desktop Notifier", alert.Title);
        Assert.Equal(Severity.Critical, alert.Severity);
        Assert.Equal("CRITICAL\nSRV-SQL01\nCPU utilization\n97.4%", alert.Body);
    }

    [Fact]
    public void Host_down_uses_host_down_headline()
    {
        var alert = IncidentAlertFormatter.From(Create(
            ProblemFactory.HostId("SRV-WEB02"),
            Severity.Critical,
            "PING CRITICAL - Host Unreachable"));

        Assert.Equal("HOST DOWN\nSRV-WEB02\nPING CRITICAL - Host Unreachable", alert.Body);
    }

    [Fact]
    public void Warning_and_unknown_headlines()
    {
        var warn = IncidentAlertFormatter.From(Create(
            ProblemFactory.ServiceId("web01", "Disk"),
            Severity.Warning,
            "80%"));
        var unknown = IncidentAlertFormatter.From(Create(
            ProblemFactory.ServiceId("web01", "Agent"),
            Severity.Unknown,
            "(null)"));
        var hostUnknown = IncidentAlertFormatter.From(Create(
            ProblemFactory.HostId("edge01"),
            Severity.Unknown,
            "UNREACHABLE"));

        Assert.StartsWith("WARNING\n", warn.Body, StringComparison.Ordinal);
        Assert.StartsWith("UNKNOWN\n", unknown.Body, StringComparison.Ordinal);
        Assert.StartsWith("HOST UNREACHABLE\n", hostUnknown.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Plugin_output_is_truncated()
    {
        var summary = new string('x', 400);
        var alert = IncidentAlertFormatter.From(Create(
            ProblemFactory.ServiceId("web01", "CPU"),
            Severity.Critical,
            summary));

        Assert.True(alert.Body.Length <= IncidentAlertFormatter.MaxBodyLength);
        Assert.Contains('…', alert.Body);
    }

    [Fact]
    public void Control_characters_are_stripped_from_summary()
    {
        var alert = IncidentAlertFormatter.From(Create(
            ProblemFactory.ServiceId("web01", "CPU"),
            Severity.Critical,
            "line1\nline2\rsecret"));

        Assert.EndsWith("line1 line2 secret", alert.Body, StringComparison.Ordinal);
    }

    private static OpenIncident Create(MonitoredObjectId id, Severity severity, string? summary) =>
        new()
        {
            ObjectId = id,
            Severity = severity,
            IsSeen = false,
            OpenedAtUtc = ProblemFactory.T0,
            LastObservedAtUtc = ProblemFactory.T0,
            LastSummary = summary
        };
}

public sealed class NotificationBaselineTests
{
    [Fact]
    public void Empty_store_without_successful_poll_is_virgin()
    {
        Assert.True(NotificationBaseline.IsVirginLocalState(0, null));
        Assert.False(NotificationBaseline.IsVirginLocalState(1, null));
        Assert.False(NotificationBaseline.IsVirginLocalState(0, ProblemFactory.T0));
        Assert.False(NotificationBaseline.IsVirginLocalState(3, ProblemFactory.T0));
    }
}
