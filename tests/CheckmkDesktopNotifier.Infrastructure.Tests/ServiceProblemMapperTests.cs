using System.Text.Json;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class ServiceProblemMapperTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.FromUnixTimeSeconds(1704067200);
    private static readonly DateTimeOffset T1 = DateTimeOffset.FromUnixTimeSeconds(1704070800);
    private static readonly DateTimeOffset TOk = DateTimeOffset.FromUnixTimeSeconds(1703980800);

    [Fact]
    public void Maps_sanitized_service_collection_fixture()
    {
        var json = FixtureReader.Read("service-collection.json");

        var problems = ServiceProblemMapper.MapCollection(json, TestOptions.Site);

        Assert.Equal(4, problems.Count);
        Assert.DoesNotContain(problems, p => p.Id.HostName == "host-e");
        Assert.All(problems, p => Assert.Equal(ObjectKind.Service, p.Id.Kind));
        Assert.All(problems, p => Assert.Equal("mysite", p.Id.SiteId.Value));
    }

    [Fact]
    public void Maps_warn_hard_service()
    {
        var warn = Find("host-a", "CPU load");

        Assert.Equal(Severity.Warning, warn.Severity);
        Assert.Equal(StateType.Hard, warn.StateType);
        Assert.Equal("load average is above threshold", warn.PluginOutput);
        Assert.Equal(T0, warn.LastStateChange);
        Assert.Equal(T1, warn.LastHardStateChange);
        Assert.Equal(TOk, warn.LastTimeOk);
        Assert.False(warn.IsAcknowledgedInCheckmk);
        Assert.Equal(0, warn.ScheduledDowntimeDepth);
        Assert.Null(warn.LastTimeUp);
    }

    [Fact]
    public void Maps_crit_hard_acknowledged_service()
    {
        var crit = Find("host-b", "Memory");

        Assert.Equal(Severity.Critical, crit.Severity);
        Assert.Equal(StateType.Hard, crit.StateType);
        Assert.True(crit.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.Sticky, crit.AcknowledgementType);
        Assert.False(crit.IsTakenByNotifier);
        Assert.Null(crit.TakenByDisplayName);
        Assert.Equal(0, crit.ScheduledDowntimeDepth);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1704153600), crit.LastStateChange);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1704157200), crit.LastHardStateChange);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1704067200), crit.LastTimeOk);
    }

    [Fact]
    public void Maps_unknown_soft_service()
    {
        var unknown = Find("host-c", "NTP time");

        Assert.Equal(Severity.Unknown, unknown.Severity);
        Assert.Equal(StateType.Soft, unknown.StateType);
        Assert.Equal("check timed out", unknown.PluginOutput);
        Assert.Null(unknown.LastHardStateChange);
        Assert.False(unknown.IsAcknowledgedInCheckmk);
    }

    [Fact]
    public void Maps_downtime_metadata()
    {
        var down = Find("host-d", "Filesystem /");

        Assert.Equal(Severity.Critical, down.Severity);
        Assert.Equal(2, down.ScheduledDowntimeDepth);
        Assert.False(down.IsAcknowledgedInCheckmk);
    }

    [Fact]
    public void Maps_unix_seconds_and_treats_zero_as_absent()
    {
        Assert.Equal(T0, UnixTimeMapper.FromUnixSeconds(1704067200));
        Assert.Null(UnixTimeMapper.FromUnixSeconds(0));
        Assert.Null(UnixTimeMapper.FromUnixSeconds(null));
        Assert.Null(UnixTimeMapper.FromUnixSeconds(-1));
        Assert.Null(UnixTimeMapper.FromUnixSeconds(long.MaxValue));
    }

    [Fact]
    public void Malformed_json_throws_protocol_exception()
    {
        var json = FixtureReader.Read("malformed-collection.json");

        var ex = Assert.Throws<CheckmkProtocolException>(
            () => ServiceProblemMapper.MapCollection(json, TestOptions.Site));

        Assert.Contains("could not be parsed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_value_array_throws_protocol_exception()
    {
        var ex = Assert.Throws<CheckmkProtocolException>(
            () => ServiceProblemMapper.MapCollection("""{"id":"all"}""", TestOptions.Site));

        Assert.Contains("value array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Result_is_core_monitored_problem_not_rest_dto()
    {
        var problem = Find("host-a", "CPU load");

        Assert.IsType<MonitoredProblem>(problem);
        Assert.Null(problem.GetType().GetProperty("Extensions"));
        Assert.Null(problem.GetType().GetProperty("DomainType"));
        Assert.DoesNotContain("extensions", problem.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Maps_cdn_take_comment_and_ignores_author()
    {
        var json = """
            {"value":[{"extensions":{
              "host_name":"HOST_R610",
              "description":"Log Veeam Backup",
              "state":2,
              "state_type":1,
              "plugin_output":"CRIT",
              "acknowledged":1,
              "acknowledgement_type":2,
              "comments_with_extra_info":[
                [36783,"ITS","Taken by mwi via Checkmk Desktop Notifier\ncdn.v1 take name=\"mwi\"",4,1787078432]
              ],
              "scheduled_downtime_depth":0
            }}]}
            """;
        var problem = Assert.Single(ServiceProblemMapper.MapCollection(json, TestOptions.Site));
        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.Sticky, problem.AcknowledgementType);
        Assert.True(problem.IsTakenByNotifier);
        Assert.Equal("mwi", problem.TakenByDisplayName);
    }

    [Fact]
    public void Maps_positional_cdn_format_comment_with_newlines()
    {
        var commentJson = JsonSerializer.Serialize(CdnTakeComment.Format("Michał"));
        var json = """
            {"value":[{"extensions":{
              "host_name":"HOST_R610",
              "description":"Log Veeam Backup",
              "state":2,
              "state_type":1,
              "plugin_output":"CRIT",
              "acknowledged":1,
              "acknowledgement_type":2,
              "comments_with_extra_info":[PLACEHOLDER],
              "scheduled_downtime_depth":0
            }}]}
            """.Replace("PLACEHOLDER", $"[36783,\"ITS\",{commentJson},4,1787078432]", StringComparison.Ordinal);
        var problem = Assert.Single(ServiceProblemMapper.MapCollection(json, TestOptions.Site));
        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.True(problem.IsTakenByNotifier);
        Assert.Equal("Michał", problem.TakenByDisplayName);
    }

    [Fact]
    public void Maps_positional_flattened_taken_by_via_comment()
    {
        var json = """
            {"value":[{"extensions":{
              "host_name":"HOST_R610",
              "description":"Log Veeam Backup",
              "state":2,
              "state_type":1,
              "plugin_output":"CRIT",
              "acknowledged":1,
              "acknowledgement_type":2,
              "comments_with_extra_info":[
                [36783,"ITS","Taken by Michał via Checkmk Desktop Notifier",4,1787078432]
              ],
              "scheduled_downtime_depth":0
            }}]}
            """;
        var problem = Assert.Single(ServiceProblemMapper.MapCollection(json, TestOptions.Site));
        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.Sticky, problem.AcknowledgementType);
        Assert.True(problem.IsTakenByNotifier);
        Assert.Equal("Michał", problem.TakenByDisplayName);
    }

    [Fact]
    public void Generic_ack_without_cdn_comment_remains_ack_not_taken()
    {
        var json = """
            {"value":[{"extensions":{
              "host_name":"web01",
              "description":"CPU",
              "state":2,
              "state_type":1,
              "plugin_output":"CRIT",
              "acknowledged":1,
              "acknowledgement_type":2,
              "comments_with_extra_info":[
                [1,"ITS","Acknowledged in GUI",4,100]
              ]
            }}]}
            """;
        var problem = Assert.Single(ServiceProblemMapper.MapCollection(json, TestOptions.Site));
        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.False(problem.IsTakenByNotifier);
        Assert.Null(problem.TakenByDisplayName);
    }

    [Fact]
    public void Acknowledged_zero_clears_taken_even_with_leftover_cdn_comment()
    {
        var json = """
            {"value":[{"extensions":{
              "host_name":"GO-S11",
              "description":"Update",
              "state":2,
              "state_type":1,
              "plugin_output":"CRIT",
              "acknowledged":0,
              "acknowledgement_type":0,
              "comments_with_extra_info":[
                [36783,"ITS","Taken by Michał via Checkmk Desktop Notifier cdn.v1 take name=\"Michał\"",4,1787078432]
              ]
            }}]}
            """;
        var problem = Assert.Single(ServiceProblemMapper.MapCollection(json, TestOptions.Site));
        Assert.False(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.None, problem.AcknowledgementType);
        Assert.False(problem.IsTakenByNotifier);
        Assert.Null(problem.TakenByDisplayName);
    }

    [Fact]
    public void Malformed_comments_do_not_crash_mapping()
    {
        var json = """
            {"value":[{"extensions":{
              "host_name":"web01",
              "description":"CPU",
              "state":2,
              "state_type":1,
              "acknowledged":1,
              "acknowledgement_type":2,
              "comments_with_extra_info":[
                "not-an-array",
                [1],
                [1,"ITS"],
                {"id":1}
              ]
            }}]}
            """;
        var problem = Assert.Single(ServiceProblemMapper.MapCollection(json, TestOptions.Site));
        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.False(problem.IsTakenByNotifier);
        Assert.Null(problem.TakenByDisplayName);
    }

    private static MonitoredProblem Find(string host, string description)
    {
        var json = FixtureReader.Read("service-collection.json");
        var problems = ServiceProblemMapper.MapCollection(json, TestOptions.Site);
        return Assert.Single(problems, p => p.Id.HostName == host && p.Id.ServiceDescription == description);
    }
}
