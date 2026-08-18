using System.Net;
using System.Text;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CdnTakeReadbackTests
{
    [Fact]
    public void Live_go_s11_positional_tuple_maps_cdn_take()
    {
        var problem = Assert.Single(
            ServiceProblemMapper.MapCollection(
                FixtureReader.Read("service-cdn-take-go-s11.json"),
                TestOptions.Site));

        Assert.Equal("GO-S11", problem.Id.HostName);
        Assert.Equal("Update", problem.Id.ServiceDescription);
        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.Sticky, problem.AcknowledgementType);
        Assert.True(problem.IsTakenByNotifier);
        Assert.Equal("Michał", problem.TakenByDisplayName);
        Assert.Equal(Severity.Critical, problem.Severity);
    }

    [Fact]
    public void Machine_tag_variant_maps_the_same_taken_by()
    {
        var problem = Assert.Single(
            ServiceProblemMapper.MapCollection(
                FixtureReader.Read("service-cdn-take-machine-tag.json"),
                TestOptions.Site));

        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.Sticky, problem.AcknowledgementType);
        Assert.True(problem.IsTakenByNotifier);
        Assert.Equal("Michał", problem.TakenByDisplayName);
    }

    [Fact]
    public void Generic_ack_fixture_is_ack_not_taken()
    {
        var problem = Assert.Single(
            ServiceProblemMapper.MapCollection(
                FixtureReader.Read("service-generic-ack.json"),
                TestOptions.Site));

        Assert.True(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.Sticky, problem.AcknowledgementType);
        Assert.False(problem.IsTakenByNotifier);
        Assert.Null(problem.TakenByDisplayName);
    }

    [Fact]
    public void Live_go_s11_survives_alert_state_read_back()
    {
        var problem = Assert.Single(
            ServiceProblemMapper.MapCollection(
                FixtureReader.Read("service-cdn-take-go-s11.json"),
                TestOptions.Site));
        var alerts = new AlertStateService(new InMemoryAlertStateStore());
        alerts.ApplySnapshot(ProblemSnapshot.Success(DateTimeOffset.UnixEpoch.AddDays(1), TestOptions.Site, [problem]));

        var open = Assert.Single(alerts.GetOpenIncidents());
        Assert.True(open.IsAcknowledgedInCheckmk);
        Assert.True(open.IsTakenByNotifier);
        Assert.Equal("Michał", open.TakenByDisplayName);
        Assert.Equal("GO-S11", open.ObjectId.HostName);
        Assert.Equal("Update", open.ObjectId.ServiceDescription);
    }

    [Fact]
    public async Task Service_client_maps_live_go_s11_take_from_http_json()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    FixtureReader.Read("service-cdn-take-go-s11.json"),
                    Encoding.UTF8,
                    "application/json")
            }
        };
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://checkmk.example.invalid/mysite/check_mk/api/1.0/")
        };
        var client = new CheckmkServiceClient(http, TestOptions.Real());

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.True(snapshot.IsSuccess);
        var problem = Assert.Single(snapshot.Problems);
        Assert.True(problem.IsTakenByNotifier);
        Assert.Equal("Michał", problem.TakenByDisplayName);
        Assert.DoesNotContain(TestOptions.Secret, handler.LastBody ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Cleared_ack_fixture_is_not_taken()
    {
        var problem = Assert.Single(
            ServiceProblemMapper.MapCollection(
                FixtureReader.Read("service-cdn-take-cleared.json"),
                TestOptions.Site));

        Assert.Equal("GO-S11", problem.Id.HostName);
        Assert.Equal("Update", problem.Id.ServiceDescription);
        Assert.False(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.None, problem.AcknowledgementType);
        Assert.False(problem.IsTakenByNotifier);
        Assert.Null(problem.TakenByDisplayName);
    }

    [Fact]
    public void Leftover_cdn_comments_with_acknowledged_zero_are_not_taken()
    {
        var problem = Assert.Single(
            ServiceProblemMapper.MapCollection(
                FixtureReader.Read("service-cdn-take-leftover-comments.json"),
                TestOptions.Site));

        Assert.False(problem.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.None, problem.AcknowledgementType);
        Assert.False(problem.IsTakenByNotifier);
        Assert.Null(problem.TakenByDisplayName);
    }

    [Fact]
    public void Taken_then_cleared_snapshot_converges_alert_state()
    {
        var taken = Assert.Single(
            ServiceProblemMapper.MapCollection(
                FixtureReader.Read("service-cdn-take-go-s11.json"),
                TestOptions.Site));
        var cleared = Assert.Single(
            ServiceProblemMapper.MapCollection(
                FixtureReader.Read("service-cdn-take-cleared.json"),
                TestOptions.Site));
        var alerts = new AlertStateService(new InMemoryAlertStateStore());
        alerts.ApplySnapshot(ProblemSnapshot.Success(DateTimeOffset.UnixEpoch.AddDays(1), TestOptions.Site, [taken]));
        alerts.MarkSeen(taken.Id);

        var delta = alerts.ApplySnapshot(ProblemSnapshot.Success(DateTimeOffset.UnixEpoch.AddDays(1).AddMinutes(1), TestOptions.Site, [cleared]));

        Assert.Empty(delta.Opened);
        var open = Assert.Single(alerts.GetOpenIncidents());
        Assert.True(open.IsSeen);
        Assert.False(open.IsAcknowledgedInCheckmk);
        Assert.Equal(AcknowledgementType.None, open.AcknowledgementType);
        Assert.False(open.IsTakenByNotifier);
        Assert.Null(open.TakenByDisplayName);
        Assert.Equal(Severity.Critical, open.Severity);
    }
}
