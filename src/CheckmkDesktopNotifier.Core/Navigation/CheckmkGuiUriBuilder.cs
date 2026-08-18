using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Navigation;

/// <summary>
/// Builds interactive Checkmk GUI URLs from the configured origin and site.
/// REST <c>urn:com.checkmk:rels/show</c> hrefs are API invoke endpoints, not browser views.
/// </summary>
public static class CheckmkGuiUriBuilder
{
    public static bool TryCreate(string? baseUrl, string? site, MonitoredObjectId? id, out Uri? uri)
    {
        uri = null;
        if (id is null
            || string.IsNullOrWhiteSpace(baseUrl)
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

        string inner;
        if (id.Kind == ObjectKind.Host)
        {
            inner = "view.py?view_name=host&host=" + Uri.EscapeDataString(id.HostName);
        }
        else if (id.Kind == ObjectKind.Service && !string.IsNullOrWhiteSpace(id.ServiceDescription))
        {
            inner = "view.py?view_name=service&host=" + Uri.EscapeDataString(id.HostName)
                    + "&service=" + Uri.EscapeDataString(id.ServiceDescription);
        }
        else
        {
            return false;
        }

        var href = origin.GetLeftPart(UriPartial.Authority).TrimEnd('/')
                   + "/" + Uri.EscapeDataString(siteName)
                   + "/check_mk/index.py?start_url="
                   + Uri.EscapeDataString(inner);

        if (!Uri.TryCreate(href, UriKind.Absolute, out var built)
            || !string.IsNullOrEmpty(built.UserInfo)
            || ContainsSecretQuery(built)
            || built.AbsolutePath.Contains("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = built;
        return true;
    }

    private static bool ContainsSecretQuery(Uri uri)
    {
        var query = uri.Query;
        return query.Contains("secret=", StringComparison.OrdinalIgnoreCase)
               || query.Contains("password=", StringComparison.OrdinalIgnoreCase)
               || query.Contains("authorization", StringComparison.OrdinalIgnoreCase)
               || query.Contains("token=", StringComparison.OrdinalIgnoreCase);
    }
}
