using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Navigation;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class CheckmkGuiUriBuilderTests
{
    private const string Origin = "https://checkmk.example.invalid";
    private const string Site = "mysite";

    [Fact]
    public void Service_navigation_opens_service_view()
    {
        var id = ProblemFactory.ServiceId("GO-S11", "Update");
        Assert.True(CheckmkGuiUriBuilder.TryCreate(Origin, Site, id, out var uri));
        Assert.NotNull(uri);
        Assert.Equal("https", uri!.Scheme);
        Assert.Equal("checkmk.example.invalid", uri.Host);
        Assert.Equal("/mysite/check_mk/index.py", uri.AbsolutePath);
        var startUrl = StartUrl(uri);
        Assert.Equal("view.py?view_name=service&host=GO-S11&service=Update", startUrl);
    }

    [Fact]
    public void Host_navigation_opens_host_view()
    {
        var id = ProblemFactory.HostId("GO-S11");
        Assert.True(CheckmkGuiUriBuilder.TryCreate(Origin, Site, id, out var uri));
        var startUrl = StartUrl(uri!);
        Assert.Equal("view.py?view_name=host&host=GO-S11", startUrl);
        Assert.DoesNotContain("service=", startUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_requiring_url_encoding_is_escaped()
    {
        var id = MonitoredObjectId.Host(ProblemFactory.DefaultSite, "GO S11/prod");
        Assert.True(CheckmkGuiUriBuilder.TryCreate(Origin, Site, id, out var uri));
        var startUrl = StartUrl(uri!);
        Assert.Equal("GO S11/prod", QueryValue(startUrl, "host"));
        Assert.DoesNotContain("GO S11/prod", uri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("GO%20S11%2Fprod", startUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_requiring_url_encoding_is_escaped()
    {
        var id = MonitoredObjectId.Service(ProblemFactory.DefaultSite, "web 01", "Filesystem /var");
        Assert.True(CheckmkGuiUriBuilder.TryCreate(Origin, Site, id, out var uri));
        var startUrl = StartUrl(uri!);
        Assert.Equal("service", QueryValue(startUrl, "view_name"));
        Assert.Equal("web 01", QueryValue(startUrl, "host"));
        Assert.Equal("Filesystem /var", QueryValue(startUrl, "service"));
        Assert.DoesNotContain("Filesystem /var", uri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("%2Fvar", startUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Configured_base_url_and_site_are_used()
    {
        var id = ProblemFactory.ServiceId("web01", "CPU");
        Assert.True(CheckmkGuiUriBuilder.TryCreate(
            "https://monitor.example.invalid:8443/",
            "itssrv",
            id,
            out var uri));
        Assert.Equal("https://monitor.example.invalid:8443/itssrv/check_mk/index.py", uri!.GetLeftPart(UriPartial.Path));
        Assert.DoesNotContain("api/1.0", uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void Url_contains_no_credentials_or_secrets()
    {
        var id = ProblemFactory.ServiceId("web01", "CPU");
        Assert.True(CheckmkGuiUriBuilder.TryCreate(Origin, Site, id, out var uri));
        Assert.True(string.IsNullOrEmpty(uri!.UserInfo));
        Assert.DoesNotContain("@", uri.Authority, StringComparison.Ordinal);
        Assert.DoesNotContain("secret=", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=", uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/", uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, "mysite")]
    [InlineData("", "mysite")]
    [InlineData("not-a-url", "mysite")]
    [InlineData("https://user:pass@checkmk.example.invalid", "mysite")]
    [InlineData("https://checkmk.example.invalid", null)]
    [InlineData("https://checkmk.example.invalid", "")]
    [InlineData("https://checkmk.example.invalid", "my/site")]
    [InlineData("ftp://checkmk.example.invalid", "mysite")]
    public void Missing_or_malformed_target_fails_safely(string? baseUrl, string? site)
    {
        var id = ProblemFactory.HostId("web01");
        Assert.False(CheckmkGuiUriBuilder.TryCreate(baseUrl, site, id, out var uri));
        Assert.Null(uri);
    }

    [Fact]
    public void Null_object_id_fails_safely()
    {
        Assert.False(CheckmkGuiUriBuilder.TryCreate(Origin, Site, null, out var uri));
        Assert.Null(uri);
    }

    private static string StartUrl(Uri uri)
    {
        const string prefix = "start_url=";
        foreach (var part in uri.Query.TrimStart('?').Split('&'))
        {
            if (!part.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            return Uri.UnescapeDataString(part[prefix.Length..]);
        }

        throw new InvalidOperationException("start_url is missing.");
    }

    private static string QueryValue(string startUrl, string key)
    {
        var queryStart = startUrl.IndexOf('?', StringComparison.Ordinal);
        Assert.True(queryStart >= 0);
        foreach (var part in startUrl[(queryStart + 1)..].Split('&'))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                continue;
            }

            if (Uri.UnescapeDataString(part[..equals]) != key)
            {
                continue;
            }

            return Uri.UnescapeDataString(part[(equals + 1)..]);
        }

        throw new InvalidOperationException($"Query key '{key}' is missing.");
    }
}
