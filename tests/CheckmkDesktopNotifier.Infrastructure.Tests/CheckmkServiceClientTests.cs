using System.Net;
using System.Text;
using System.Text.Json;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Authentication;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkServiceClientTests
{
    [Fact]
    public async Task Posts_verified_service_collection_query_with_auth_and_json_headers()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, FixtureReader.Read("service-collection.json"))
        };
        var client = CreateClient(handler);

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.True(snapshot.IsSuccess);
        Assert.Equal(200, client.LastHttpStatusCode);
        Assert.Equal(4, snapshot.Problems.Count);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://checkmk.example.invalid/mysite/check_mk/api/1.0/domain-types/service/collections/all",
            request.RequestUri?.ToString());
        Assert.Contains(request.Headers.Accept, h => h.MediaType == "application/json");
        Assert.Equal(
            CheckmkAuthenticationHeader.CreateValue("automation", TestOptions.Secret),
            request.Headers.GetValues("Authorization").Single());
        Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(handler.LastBody!);
        var columns = body.RootElement.GetProperty("columns").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("host_name", columns);
        Assert.Contains("description", columns);
        Assert.Contains("state", columns);
        Assert.Contains("state_type", columns);
        Assert.Contains("plugin_output", columns);
        Assert.Contains("last_state_change", columns);
        Assert.Contains("last_hard_state_change", columns);
        Assert.Contains("last_time_ok", columns);
        Assert.Contains("acknowledged", columns);
        Assert.Contains("acknowledgement_type", columns);
        Assert.Contains("comments_with_extra_info", columns);
        Assert.Contains("scheduled_downtime_depth", columns);
        Assert.Equal("or", body.RootElement.GetProperty("query").GetProperty("op").GetString());
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

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.False(snapshot.IsSuccess);
        Assert.Equal(SnapshotErrorKind.Unavailable, snapshot.ErrorKind);
        Assert.Equal(500, client.LastHttpStatusCode);
        Assert.Empty(snapshot.Problems);
        Assert.DoesNotContain("upstream error", snapshot.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_401_is_authentication_failure()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        };
        var client = CreateClient(handler);

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.False(snapshot.IsSuccess);
        Assert.Equal(SnapshotErrorKind.Authentication, snapshot.ErrorKind);
        Assert.Equal(401, client.LastHttpStatusCode);
    }

    [Fact]
    public async Task Malformed_success_body_is_protocol_failure()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, FixtureReader.Read("malformed-collection.json"))
        };
        var client = CreateClient(handler);

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.False(snapshot.IsSuccess);
        Assert.Equal(SnapshotErrorKind.Protocol, snapshot.ErrorKind);
        Assert.Equal(200, client.LastHttpStatusCode);
    }

    [Fact]
    public async Task Network_failure_does_not_include_authorization_or_secret()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => throw new HttpRequestException($"Bearer automation {TestOptions.Secret} failed")
        };
        var client = CreateClient(handler);

        var snapshot = await client.GetCurrentProblemsAsync();

        Assert.False(snapshot.IsSuccess);
        Assert.Equal(SnapshotErrorKind.Unavailable, snapshot.ErrorKind);
        Assert.Equal("The Checkmk server cannot be reached.", snapshot.ErrorMessage);
        Assert.DoesNotContain(TestOptions.Secret, snapshot.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", snapshot.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static CheckmkServiceClient CreateClient(RecordingHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = TestOptions.Real().CreateApiBaseUri()
        };
        return new CheckmkServiceClient(http, TestOptions.Real());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
