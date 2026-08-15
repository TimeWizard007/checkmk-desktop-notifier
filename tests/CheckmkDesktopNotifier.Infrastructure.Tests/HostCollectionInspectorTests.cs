using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class HostCollectionInspectorTests
{
    [Fact]
    public void Name_only_collection_reports_missing_state_fields()
    {
        var inspection = HostCollectionInspector.Inspect(FixtureReader.Read("host-collection-name-only.json"));

        Assert.Equal(2, inspection.HostCount);
        Assert.False(inspection.StateAvailable);
        Assert.Null(inspection.UpCount);
        Assert.Null(inspection.DownCount);
        Assert.Null(inspection.UnreachableCount);
        Assert.Equal("extensions.name", inspection.IdentitySource);
        Assert.Contains("name", inspection.PresentMonitoringFields);
        Assert.Contains("state", inspection.MissingMonitoringFields);
        Assert.Contains("state_type", inspection.MissingMonitoringFields);
        Assert.Contains("last_time_up", inspection.MissingMonitoringFields);
        Assert.Contains("columns=", inspection.NextRuntimeTest, StringComparison.Ordinal);
        Assert.Contains("Do not use host_config", inspection.NextRuntimeTest, StringComparison.Ordinal);
        Assert.Contains("Do not invent a host POST", inspection.NextRuntimeTest, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_collection_counts_up_down_and_unreachable()
    {
        var inspection = HostCollectionInspector.Inspect(FixtureReader.Read("host-collection-status.json"));

        Assert.Equal(6, inspection.HostCount);
        Assert.True(inspection.StateAvailable);
        Assert.Equal(1, inspection.UpCount);
        Assert.Equal(3, inspection.DownCount);
        Assert.Equal(2, inspection.UnreachableCount);
        Assert.Contains("state", inspection.PresentMonitoringFields);
        Assert.Contains("state_type", inspection.PresentMonitoringFields);
        Assert.Contains("last_time_up", inspection.PresentMonitoringFields);
        Assert.Contains("acknowledged", inspection.PresentMonitoringFields);
        Assert.Contains("scheduled_downtime_depth", inspection.PresentMonitoringFields);
        Assert.Empty(inspection.MissingMonitoringFields);
    }

    [Fact]
    public void Malformed_json_throws_protocol_exception()
    {
        Assert.Throws<CheckmkProtocolException>(
            () => HostCollectionInspector.Inspect(FixtureReader.Read("malformed-host-collection.json")));
    }
}
