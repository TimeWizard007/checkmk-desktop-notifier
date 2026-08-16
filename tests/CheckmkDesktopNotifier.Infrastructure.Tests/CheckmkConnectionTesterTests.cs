using System.Net;
using System.Security.Authentication;
using System.Text;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CheckmkConnectionTesterTests
{
    [Fact]
    public async Task Success_reports_services_and_hosts_reachable()
    {
        var handler = new RecordingHandler { Responder = Combined };
        var tester = new CheckmkConnectionTester(handler: handler);

        var result = await tester.TestAsync(TestOptions.Real());

        Assert.Equal(ConnectionTestStatus.Success, result.Status);
        Assert.True(result.ServicesReachable);
        Assert.True(result.HostsReachable);
        Assert.DoesNotContain("Bearer", result.UserMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestOptions.Secret, result.UserMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Classifies_401()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        };
        var result = await new CheckmkConnectionTester(handler: handler).TestAsync(TestOptions.Real());
        Assert.Equal(ConnectionTestStatus.Unauthorized, result.Status);
        Assert.DoesNotContain(TestOptions.Secret, result.UserMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", result.UserMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Classifies_403()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        };
        var result = await new CheckmkConnectionTester(handler: handler).TestAsync(TestOptions.Real());
        Assert.Equal(ConnectionTestStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task Classifies_unreachable()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => throw new HttpRequestException(
                "failed",
                new System.Net.Sockets.SocketException())
        };
        var result = await new CheckmkConnectionTester(handler: handler).TestAsync(TestOptions.Real());
        Assert.Equal(ConnectionTestStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task Classifies_timeout()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => throw new TaskCanceledException("timeout")
        };
        var result = await new CheckmkConnectionTester(handler: handler).TestAsync(TestOptions.Real());
        Assert.Equal(ConnectionTestStatus.Timeout, result.Status);
    }

    [Fact]
    public async Task Classifies_tls_error()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => throw new HttpRequestException(
                "ssl",
                new AuthenticationException("cert"))
        };
        var result = await new CheckmkConnectionTester(handler: handler).TestAsync(TestOptions.Real());
        Assert.Equal(ConnectionTestStatus.TlsError, result.Status);
    }

    [Fact]
    public async Task Classifies_malformed_api_response()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json")
            }
        };
        var result = await new CheckmkConnectionTester(handler: handler).TestAsync(TestOptions.Real());
        Assert.Equal(ConnectionTestStatus.UnexpectedApiResponse, result.Status);
    }

    [Fact]
    public async Task Classifies_invalid_configuration()
    {
        var options = new CheckmkOptions { Mode = ClientMode.Real, PollIntervalSeconds = 60 };
        var result = await new CheckmkConnectionTester().TestAsync(options);
        Assert.Equal(ConnectionTestStatus.InvalidConfiguration, result.Status);
    }

    private static HttpResponseMessage Combined(HttpRequestMessage request)
    {
        var json = request.Method == HttpMethod.Post
            ? FixtureReader.Read("service-collection.json")
            : FixtureReader.Read("host-collection-status.json");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
