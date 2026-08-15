using System.Net;
using System.Text;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Authentication;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkHostClientTests
{
    [Fact]
    public async Task Verified_probe_is_get_without_query_string()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, FixtureReader.Read("host-collection-name-only.json"))
        };
        var client = CreateClient(handler);

        var result = await client.ProbeVerifiedAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal(2, result.Inspection!.HostCount);
        Assert.False(result.Inspection.StateAvailable);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "https://checkmk.example.invalid/mysite/check_mk/api/1.0/domain-types/host/collections/all",
            request.RequestUri?.ToString());
        Assert.True(string.IsNullOrEmpty(request.RequestUri?.Query));
        Assert.Null(request.Content);
        Assert.Contains(request.Headers.Accept, h => h.MediaType == "application/json");
        Assert.Equal(
            CheckmkAuthenticationHeader.CreateValue("automation", TestOptions.Secret),
            request.Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task Documented_columns_probe_uses_repeated_query_string_not_json_body()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, FixtureReader.Read("host-collection-status.json"))
        };
        var client = CreateClient(handler);

        var result = await client.ProbeDocumentedColumnsAsync();

        Assert.True(result.IsSuccess);
        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Null(handler.LastRequest.Content);
        Assert.Contains("columns=name", uri, StringComparison.Ordinal);
        Assert.Contains("columns=state", uri, StringComparison.Ordinal);
        Assert.Contains("columns=state_type", uri, StringComparison.Ordinal);
        Assert.Contains("columns=last_time_up", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("query=", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("host_config", uri, StringComparison.Ordinal);
        Assert.Equal(6, result.Inspection!.HostCount);
        Assert.True(result.Inspection.StateAvailable);
    }

    [Fact]
    public async Task Http_500_is_unavailable_failure()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("upstream error", Encoding.UTF8, "text/plain")
            }
        };
        var client = CreateClient(handler);

        var result = await client.ProbeVerifiedAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(SnapshotErrorKind.Unavailable, result.ErrorKind);
        Assert.Equal(500, result.HttpStatusCode);
        Assert.DoesNotContain("upstream error", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_success_body_is_protocol_failure()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, FixtureReader.Read("malformed-host-collection.json"))
        };
        var client = CreateClient(handler);

        var result = await client.ProbeVerifiedAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(SnapshotErrorKind.Protocol, result.ErrorKind);
        Assert.Equal(200, result.HttpStatusCode);
    }

    private static CheckmkHostClient CreateClient(RecordingHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = TestOptions.Real().CreateApiBaseUri()
        };
        return new CheckmkHostClient(http, TestOptions.Real());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
