using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class HostProblemMapperTests
{
    [Fact]
    public void Name_only_collection_does_not_invent_host_problems()
    {
        var problems = HostProblemMapper.MapCollection(
            FixtureReader.Read("host-collection-name-only.json"),
            TestOptions.Site);

        Assert.Empty(problems);
    }

    [Fact]
    public void Maps_host_identity_without_service_description()
    {
        var problem = Find("host-down-hard");

        Assert.Equal(ObjectKind.Host, problem.Id.Kind);
        Assert.Equal("host-down-hard", problem.Id.HostName);
        Assert.Null(problem.Id.ServiceDescription);
        Assert.Equal("mysite", problem.Id.SiteId.Value);
    }

    [Fact]
    public void Maps_down_to_critical()
    {
        var problem = Find("host-down-hard");

        Assert.Equal(Severity.Critical, problem.Severity);
        Assert.Equal(StateType.Hard, problem.StateType);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1704067200), problem.LastTimeUp);
        Assert.Equal(problem.LastTimeUp, problem.RecurrenceMarker);
        Assert.Null(problem.LastTimeOk);
    }

    [Fact]
    public void Maps_unreachable_to_unknown()
    {
        var problem = Find("host-unreach-hard");

        Assert.Equal(Severity.Unknown, problem.Severity);
        Assert.Equal(StateType.Hard, problem.StateType);
        Assert.Equal(2, problem.ScheduledDowntimeDepth);
    }

    [Fact]
    public void Maps_soft_but_hard_filter_ignores_it()
    {
        var all = HostProblemMapper.MapCollection(
            FixtureReader.Read("host-collection-status.json"),
            TestOptions.Site);
        var hard = HostProblemMapper.MapHardProblems(
            FixtureReader.Read("host-collection-status.json"),
            TestOptions.Site);

        Assert.Contains(all, p => p.Id.HostName == "host-down-soft" && p.StateType == StateType.Soft);
        Assert.Contains(all, p => p.Id.HostName == "host-unreach-soft" && p.StateType == StateType.Soft);
        Assert.DoesNotContain(hard, p => p.StateType == StateType.Soft);
        Assert.All(hard, p => Assert.Equal(StateType.Hard, p.StateType));
        Assert.Equal(3, hard.Count);
    }

    [Fact]
    public void Maps_ack_metadata()
    {
        var problem = Find("host-down-ack");

        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(0, problem.ScheduledDowntimeDepth);
        Assert.Equal(Severity.Critical, problem.Severity);
    }

    [Fact]
    public void Skips_up_hosts()
    {
        var problems = HostProblemMapper.MapCollection(
            FixtureReader.Read("host-collection-status.json"),
            TestOptions.Site);

        Assert.DoesNotContain(problems, p => p.Id.HostName == "host-up");
    }

    [Fact]
    public void Malformed_json_throws_protocol_exception()
    {
        Assert.Throws<CheckmkProtocolException>(
            () => HostProblemMapper.MapCollection(
                FixtureReader.Read("malformed-host-collection.json"),
                TestOptions.Site));
    }

    private static MonitoredProblem Find(string hostName)
    {
        var problems = HostProblemMapper.MapCollection(
            FixtureReader.Read("host-collection-status.json"),
            TestOptions.Site);
        return Assert.Single(problems, p => p.Id.HostName == hostName);
    }
}
