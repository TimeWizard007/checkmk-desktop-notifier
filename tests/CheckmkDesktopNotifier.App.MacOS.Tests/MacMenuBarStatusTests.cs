using CheckmkDesktopNotifier.App.MacOS;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Polling;

namespace CheckmkDesktopNotifier.App.MacOS.Tests;

public sealed class MacMenuBarStatusTests
{
    [Fact]
    public void Counts_match_new_severity_and_taken()
    {
        var counts = MacMenuBarStatus.FromIncidents(
        [
            Incident("web01", "CPU", Severity.Critical, seen: false, taken: false),
            Incident("web01", "Disk", Severity.Critical, seen: true, taken: true),
            Incident("app01", "Load", Severity.Warning, seen: false, taken: false),
            Incident("mail01", "IMAP", Severity.Unknown, seen: false, taken: false)
        ]);

        Assert.Equal(3, counts.New);
        Assert.Equal(2, counts.Critical);
        Assert.Equal(1, counts.Warning);
        Assert.Equal(1, counts.Unknown);
        Assert.Equal(1, counts.Taken);
    }

    [Fact]
    public void Title_uses_compact_count_form_when_connected()
    {
        var title = MacMenuBarStatus.FormatTitle(
            new MacMenuBarCounts(25, 98, 21, 3, 1),
            MacMenuBarConnectionState.Connected);
        Assert.Equal("N:25 C:98 W:21 U:3 T:1", title);
    }

    [Fact]
    public void Title_drops_taken_then_unknown_when_too_long()
    {
        var title = MacMenuBarStatus.FormatTitle(
            new MacMenuBarCounts(1250, 3400, 2100, 800, 400),
            MacMenuBarConnectionState.Connected);
        Assert.DoesNotContain("T:", title, StringComparison.Ordinal);
        Assert.StartsWith("N:", title, StringComparison.Ordinal);
        Assert.True(title.Length <= MacMenuBarStatus.MaxTitleLength);
    }

    [Fact]
    public void Connection_state_projection()
    {
        Assert.Equal(
            MacMenuBarConnectionState.NotConfigured,
            MacMenuBarStatus.FromSession(false, ConnectionStatus.Idle));
        Assert.Equal(
            MacMenuBarConnectionState.Disconnected,
            MacMenuBarStatus.FromSession(true, ConnectionStatus.Idle));
        Assert.Equal(
            MacMenuBarConnectionState.Connected,
            MacMenuBarStatus.FromSession(true, new ConnectionStatus(ConnectionStatusKind.Connected, null, null)));
        Assert.Equal(
            MacMenuBarConnectionState.Connected,
            MacMenuBarStatus.FromSession(true, new ConnectionStatus(ConnectionStatusKind.Refreshing, null, null)));
        Assert.Equal(
            MacMenuBarConnectionState.Error,
            MacMenuBarStatus.FromSession(true, new ConnectionStatus(ConnectionStatusKind.Error, null, "down")));
    }

    [Fact]
    public void Error_and_unconfigured_titles()
    {
        Assert.Equal("Checkmk", MacMenuBarStatus.FormatTitle(default, MacMenuBarConnectionState.NotConfigured));
        Assert.Equal("Checkmk · —", MacMenuBarStatus.FormatTitle(default, MacMenuBarConnectionState.Disconnected));
        Assert.StartsWith("!", MacMenuBarStatus.FormatTitle(
            new MacMenuBarCounts(1, 0, 0, 0, 0),
            MacMenuBarConnectionState.Error), StringComparison.Ordinal);
        Assert.Contains("Connection error", MacMenuBarStatus.FormatToolTip(default, MacMenuBarConnectionState.Error), StringComparison.Ordinal);
        Assert.Equal("Connected", MacMenuBarStatus.FormatConnectionLabel(MacMenuBarConnectionState.Connected));
        Assert.Equal("Disconnected", MacMenuBarStatus.FormatConnectionLabel(MacMenuBarConnectionState.Disconnected));
    }

    [Fact]
    public void Startup_policy_opens_settings_only_when_unconfigured()
    {
        Assert.True(MacStartupPolicy.ShowSettingsOnStartup(true));
        Assert.False(MacStartupPolicy.ShowSettingsOnStartup(false));
        Assert.True(MacStartupPolicy.StartPollingOnStartup(true));
        Assert.False(MacStartupPolicy.StartPollingOnStartup(false));
    }

    private static OpenIncident Incident(
        string host,
        string service,
        Severity severity,
        bool seen,
        bool taken)
    {
        return new OpenIncident
        {
            ObjectId = MonitoredObjectId.Service(new SiteId("site"), host, service),
            Severity = severity,
            IsSeen = seen,
            OpenedAtUtc = DateTimeOffset.UnixEpoch,
            LastObservedAtUtc = DateTimeOffset.UnixEpoch,
            IsTakenByNotifier = taken,
            IsAcknowledgedInCheckmk = taken
        };
    }
}
