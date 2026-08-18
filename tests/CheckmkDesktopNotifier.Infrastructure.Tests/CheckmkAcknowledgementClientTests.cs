using System.Net;
using System.Text.Json;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Infrastructure.Authentication;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkAcknowledgementClientTests
{
    [Fact]
    public async Task Service_ack_posts_validated_payload()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent)
        };
        var client = CreateClient(handler);

        var result = await client.AcknowledgeServiceAsync("HOST_R610", "Log Veeam Backup", "mwi");

        Assert.Equal(AcknowledgementWriteStatus.Success, result.Status);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(
            "https://checkmk.example.invalid/mysite/check_mk/api/1.0/domain-types/acknowledge/collections/service",
            handler.LastRequest.RequestUri?.ToString());
        Assert.Equal(
            CheckmkAuthenticationHeader.CreateValue("automation", TestOptions.Secret),
            handler.LastRequest.Headers.GetValues("Authorization").Single());

        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        Assert.True(root.GetProperty("sticky").GetBoolean());
        Assert.False(root.GetProperty("persistent").GetBoolean());
        Assert.False(root.GetProperty("notify").GetBoolean());
        Assert.Equal("service", root.GetProperty("acknowledge_type").GetString());
        Assert.Equal("HOST_R610", root.GetProperty("host_name").GetString());
        Assert.Equal("Log Veeam Backup", root.GetProperty("service_description").GetString());
        Assert.Equal(
            CdnTakeComment.Format("mwi"),
            root.GetProperty("comment").GetString());
        Assert.False(root.TryGetProperty("expire_on", out _));
        Assert.DoesNotContain(TestOptions.Secret, handler.LastBody!, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestOptions.Secret, result.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_ack_uses_host_endpoint_and_payload()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent)
        };
        var client = CreateClient(handler);

        var result = await client.AcknowledgeHostAsync("HOST_R610", "Michał");

        Assert.Equal(AcknowledgementWriteStatus.Success, result.Status);
        Assert.Equal(
            "https://checkmk.example.invalid/mysite/check_mk/api/1.0/domain-types/acknowledge/collections/host",
            handler.LastRequest!.RequestUri?.ToString());
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("host", body.RootElement.GetProperty("acknowledge_type").GetString());
        Assert.Equal("HOST_R610", body.RootElement.GetProperty("host_name").GetString());
        Assert.False(body.RootElement.TryGetProperty("service_description", out _));
        Assert.False(body.RootElement.TryGetProperty("expire_on", out _));
        Assert.Contains("cdn.v1 take name=\"Michał\"", body.RootElement.GetProperty("comment").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_ack_http_body_contains_full_cdn_comment_for_michal()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent)
        };
        var client = CreateClient(handler);

        var result = await client.AcknowledgeServiceAsync("GO-S11", "Update", "Michał");

        Assert.Equal(AcknowledgementWriteStatus.Success, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(handler.LastBody));
        Assert.DoesNotContain("Authorization", handler.LastBody!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestOptions.Secret, handler.LastBody!, StringComparison.Ordinal);

        const string expected =
            "Taken by Michał via Checkmk Desktop Notifier cdn.v1 take name=\"Michał\"";
        Assert.Equal(expected, CdnTakeComment.Format("Michał"));

        using var body = JsonDocument.Parse(handler.LastBody!);
        var comment = body.RootElement.GetProperty("comment").GetString();
        Assert.Equal(expected, comment);
        Assert.Contains("Taken by Michał", comment, StringComparison.Ordinal);
        Assert.Contains("via Checkmk Desktop Notifier", comment, StringComparison.Ordinal);
        Assert.Contains("cdn.v1 take name=\"Michał\"", comment, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', comment!);
        Assert.DoesNotContain('\r', comment!);

        Assert.Contains("Taken by Michał", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("via Checkmk Desktop Notifier", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("cdn.v1 take name=\\\"Michał\\\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Equal("service", body.RootElement.GetProperty("acknowledge_type").GetString());
        Assert.Equal("GO-S11", body.RootElement.GetProperty("host_name").GetString());
        Assert.Equal("Update", body.RootElement.GetProperty("service_description").GetString());
        Assert.True(body.RootElement.GetProperty("sticky").GetBoolean());
        Assert.False(body.RootElement.GetProperty("persistent").GetBoolean());
        Assert.False(body.RootElement.GetProperty("notify").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("expire_on", out _));
    }

    [Fact]
    public async Task Host_ack_http_body_contains_full_cdn_comment_for_michal()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent)
        };
        var client = CreateClient(handler);

        var result = await client.AcknowledgeHostAsync("GO-S11", "Michał");

        Assert.Equal(AcknowledgementWriteStatus.Success, result.Status);
        using var body = JsonDocument.Parse(handler.LastBody!);
        var comment = body.RootElement.GetProperty("comment").GetString();
        Assert.Equal(
            "Taken by Michał via Checkmk Desktop Notifier cdn.v1 take name=\"Michał\"",
            comment);
        Assert.Contains("cdn.v1 take name=\\\"Michał\\\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Equal("host", body.RootElement.GetProperty("acknowledge_type").GetString());
        Assert.DoesNotContain(TestOptions.Secret, handler.LastBody!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AcknowledgementWriteStatus.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, AcknowledgementWriteStatus.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest, AcknowledgementWriteStatus.InvalidRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity, AcknowledgementWriteStatus.InvalidRequest)]
    public async Task Maps_http_failures(HttpStatusCode status, AcknowledgementWriteStatus expected)
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"secret":"must-not-leak"}""")
            }
        };
        var client = CreateClient(handler);
        var result = await client.AcknowledgeServiceAsync("web01", "CPU", "Michał");
        Assert.Equal(expected, result.Status);
        Assert.DoesNotContain("must-not-leak", result.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(TestOptions.Secret, result.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{", result.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timeout_is_unavailable_without_secret()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => throw new TaskCanceledException("timeout")
        };
        var client = CreateClient(handler);
        var result = await client.AcknowledgeHostAsync("web01", "Michał");
        Assert.Equal(AcknowledgementWriteStatus.Unavailable, result.Status);
        Assert.DoesNotContain(TestOptions.Secret, result.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_release_posts_validated_payload()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent)
        };
        var client = CreateClient(handler);

        var result = await client.DeleteServiceAsync("GO-S01", "Update");

        Assert.Equal(AcknowledgementWriteStatus.Success, result.Status);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(
            "https://checkmk.example.invalid/mysite/check_mk/api/1.0/domain-types/acknowledge/actions/delete/invoke",
            handler.LastRequest.RequestUri?.ToString());
        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        Assert.Equal("service", root.GetProperty("acknowledge_type").GetString());
        Assert.Equal("GO-S01", root.GetProperty("host_name").GetString());
        Assert.Equal("Update", root.GetProperty("service_description").GetString());
        Assert.False(root.TryGetProperty("comment", out _));
        Assert.False(root.TryGetProperty("sticky", out _));
        Assert.False(root.TryGetProperty("expire_on", out _));
        Assert.DoesNotContain(TestOptions.Secret, handler.LastBody!, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Host_release_uses_same_endpoint_without_service()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent)
        };
        var client = CreateClient(handler);

        var result = await client.DeleteHostAsync("GO-S01");

        Assert.Equal(AcknowledgementWriteStatus.Success, result.Status);
        Assert.Equal(
            "https://checkmk.example.invalid/mysite/check_mk/api/1.0/domain-types/acknowledge/actions/delete/invoke",
            handler.LastRequest!.RequestUri?.ToString());
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("host", body.RootElement.GetProperty("acknowledge_type").GetString());
        Assert.Equal("GO-S01", body.RootElement.GetProperty("host_name").GetString());
        Assert.False(body.RootElement.TryGetProperty("service_description", out _));
        Assert.False(body.RootElement.TryGetProperty("comment", out _));
        Assert.DoesNotContain(TestOptions.Secret, handler.LastBody!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AcknowledgementWriteStatus.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, AcknowledgementWriteStatus.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest, AcknowledgementWriteStatus.InvalidRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity, AcknowledgementWriteStatus.InvalidRequest)]
    public async Task Delete_maps_http_failures(HttpStatusCode status, AcknowledgementWriteStatus expected)
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"secret":"must-not-leak"}""")
            }
        };
        var client = CreateClient(handler);
        var result = await client.DeleteServiceAsync("web01", "CPU");
        Assert.Equal(expected, result.Status);
        Assert.DoesNotContain("must-not-leak", result.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(TestOptions.Secret, result.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_timeout_is_unavailable_without_secret()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => throw new TaskCanceledException("timeout")
        };
        var client = CreateClient(handler);
        var result = await client.DeleteHostAsync("web01");
        Assert.Equal(AcknowledgementWriteStatus.Unavailable, result.Status);
        Assert.DoesNotContain(TestOptions.Secret, result.UserMessage, StringComparison.Ordinal);
    }

    private static CheckmkAcknowledgementClient CreateClient(RecordingHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://checkmk.example.invalid/mysite/check_mk/api/1.0/")
        };
        return new CheckmkAcknowledgementClient(http, TestOptions.Real());
    }
}
