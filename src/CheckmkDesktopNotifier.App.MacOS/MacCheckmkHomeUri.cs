namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// Site home URL for the M1 "Open Checkmk" action. Does not change
/// <c>CheckmkGuiUriBuilder</c> (host/service views).
/// </summary>
public static class MacCheckmkHomeUri
{
    public static bool TryCreate(string? baseUrl, string? site, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(baseUrl)
            || string.IsNullOrWhiteSpace(site)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(origin.UserInfo)
            || string.IsNullOrWhiteSpace(origin.Host))
        {
            return false;
        }

        var siteName = site.Trim().Trim('/');
        if (siteName.Length == 0
            || siteName.Contains('/', StringComparison.Ordinal)
            || siteName.Contains('\\', StringComparison.Ordinal)
            || siteName.Contains('?', StringComparison.Ordinal)
            || siteName.Contains('#', StringComparison.Ordinal)
            || siteName.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        var href = origin.GetLeftPart(UriPartial.Authority).TrimEnd('/')
                   + "/" + Uri.EscapeDataString(siteName)
                   + "/check_mk/";
        if (!Uri.TryCreate(href, UriKind.Absolute, out var built)
            || !string.IsNullOrEmpty(built.UserInfo))
        {
            return false;
        }

        uri = built;
        return true;
    }
}
