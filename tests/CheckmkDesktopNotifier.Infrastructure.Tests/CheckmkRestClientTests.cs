using System.Net;
using System.Text;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkRestClientTests
{
    [Fact]
    public async Task Merges_service_problems_with_hard_host_down_and_unreachable()
    {
        var handler = new RecordingHandler { Responder = CombinedResponder };
        var client = CreateClient(handler);

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.True(snapshot.IsSuccess);
        Assert.Equal(4, snapshot.Problems.Count(p => p.Id.Kind == ObjectKind.Service));
        Assert.Equal(3, snapshot.Problems.Count(p => p.Id.Kind == ObjectKind.Host));
        Assert.All(
            snapshot.Problems.Where(p => p.Id.Kind == ObjectKind.Host),
            p => Assert.Equal(StateType.Hard, p.StateType));
        Assert.Contains(snapshot.Problems, p => p.Id.Kind == ObjectKind.Host && p.Id.HostName == "host-down-hard" && p.Severity == Severity.Critical);
        Assert.Contains(snapshot.Problems, p => p.Id.Kind == ObjectKind.Host && p.Id.HostName == "host-unreach-hard" && p.Severity == Severity.Unknown);
        Assert.DoesNotContain(snapshot.Problems, p => p.Id.HostName == "host-up");
        Assert.DoesNotContain(snapshot.Problems, p => p.Id.HostName == "host-down-soft");
        Assert.DoesNotContain(snapshot.Problems, p => p.Id.Kind == ObjectKind.Host && p.Id.ServiceDescription is not null);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("/domain-types/service/collections/all", handler.Requests[0].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        var hostUri = handler.Requests[1].RequestUri!.ToString();
        Assert.Contains("/domain-types/host/collections/all", hostUri, StringComparison.Ordinal);
        Assert.Contains("columns=name", hostUri, StringComparison.Ordinal);
        Assert.Contains("columns=state", hostUri, StringComparison.Ordinal);
        Assert.Contains("columns=last_time_up", hostUri, StringComparison.Ordinal);
        Assert.DoesNotContain("query=", hostUri, StringComparison.Ordinal);
        Assert.DoesNotContain("host_config", hostUri, StringComparison.Ordinal);
        Assert.Null(handler.Requests[1].Content);
    }

    [Fact]
    public async Task Host_http_failure_fails_the_whole_snapshot()
    {
        var handler = new RecordingHandler
        {
            Responder = request => request.Method == HttpMethod.Post
                ? Json(HttpStatusCode.OK, FixtureReader.Read("service-collection.json"))
                : new HttpResponseMessage(HttpStatusCode.InternalServerError)
        };
        var client = CreateClient(handler);

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.False(snapshot.IsSuccess);
        Assert.Equal(SnapshotErrorKind.Unavailable, snapshot.ErrorKind);
        Assert.Empty(snapshot.Problems);
    }

    [Fact]
    public async Task Service_http_failure_does_not_fetch_hosts()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        };
        var client = CreateClient(handler);

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.False(snapshot.IsSuccess);
        Assert.Equal(SnapshotErrorKind.Authentication, snapshot.ErrorKind);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    }

    [Fact]
    public async Task GetHardHostProblems_uses_columns_query_string()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, FixtureReader.Read("host-collection-status.json"))
        };
        var hosts = new CheckmkHostClient(
            new HttpClient(handler) { BaseAddress = TestOptions.Real().CreateApiBaseUri() },
            TestOptions.Real());

        var snapshot = await hosts.GetHardHostProblemsAsync();

        Assert.True(snapshot.IsSuccess);
        Assert.Equal(3, snapshot.Problems.Count);
        Assert.All(snapshot.Problems, p => Assert.Equal(ObjectKind.Host, p.Id.Kind));
        Assert.Contains("columns=state_type", handler.LastRequest!.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    private static CheckmkRestClient CreateClient(RecordingHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = TestOptions.Real().CreateApiBaseUri() },
            TestOptions.Real());

    private static HttpResponseMessage CombinedResponder(HttpRequestMessage request)
    {
        if (request.Method == HttpMethod.Post)
        {
            return Json(HttpStatusCode.OK, FixtureReader.Read("service-collection.json"));
        }

        return Json(HttpStatusCode.OK, FixtureReader.Read("host-collection-status.json"));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
