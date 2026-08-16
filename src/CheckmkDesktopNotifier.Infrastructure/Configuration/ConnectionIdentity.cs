using System.Security.Cryptography;
using System.Text;

namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public sealed record ConnectionIdentity(string NormalizedBaseUrl, string Site)
{
    public static ConnectionIdentity From(string baseUrl, string site)
    {
        var normalized = CheckmkOptionsValidator.NormalizeBaseUrl(baseUrl);
        if (string.IsNullOrWhiteSpace(site) || site.Contains('/', StringComparison.Ordinal))
        {
            throw new CheckmkOptionsValidationException("Site must be the Checkmk site name (for example mysite).");
        }

        return new ConnectionIdentity(normalized, site.Trim());
    }

    public string FileId
    {
        get
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{NormalizedBaseUrl}\n{Site}"));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }

    public bool EqualsIdentity(ConnectionIdentity? other) =>
        other is not null
        && string.Equals(NormalizedBaseUrl, other.NormalizedBaseUrl, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Site, other.Site, StringComparison.Ordinal);
}
