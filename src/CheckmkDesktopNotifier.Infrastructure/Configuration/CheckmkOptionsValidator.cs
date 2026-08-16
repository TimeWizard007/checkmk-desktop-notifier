namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public sealed class CheckmkOptionsValidationException : Exception
{
    public CheckmkOptionsValidationException(string message)
        : base(message)
    {
    }
}

public static class CheckmkOptionsValidator
{
    public static void Validate(CheckmkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PollIntervalSeconds < CheckmkOptions.MinimumPollIntervalSeconds)
        {
            throw new CheckmkOptionsValidationException(
                $"PollIntervalSeconds must be at least {CheckmkOptions.MinimumPollIntervalSeconds}.");
        }

        if (options.Mode != ClientMode.Real)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl)
            || !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new CheckmkOptionsValidationException(
                "BaseUrl must be an absolute http or https URL without credentials.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new CheckmkOptionsValidationException("BaseUrl must not contain a username or password.");
        }

        RejectApiPathInBaseUrl(uri);

        if (string.IsNullOrWhiteSpace(options.Site) || options.Site.Contains('/', StringComparison.Ordinal))
        {
            throw new CheckmkOptionsValidationException("Site must be the Checkmk site name (for example itssrv).");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new CheckmkOptionsValidationException("Username is required for Real mode.");
        }

        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new CheckmkOptionsValidationException("Secret is required for Real mode.");
        }
    }

    public static void ValidateGui(CheckmkOptions options, bool requireSecret)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PollIntervalSeconds < CheckmkOptions.MinimumPollIntervalSeconds)
        {
            throw new CheckmkOptionsValidationException(
                $"PollIntervalSeconds must be at least {CheckmkOptions.MinimumPollIntervalSeconds}.");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl)
            || !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new CheckmkOptionsValidationException("BaseUrl must be an absolute https URL without credentials.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new CheckmkOptionsValidationException("BaseUrl must not contain a username or password.");
        }

        RejectApiPathInBaseUrl(uri);

        if (string.IsNullOrWhiteSpace(options.Site) || options.Site.Contains('/', StringComparison.Ordinal))
        {
            throw new CheckmkOptionsValidationException("Site must be the Checkmk site name (for example mysite).");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new CheckmkOptionsValidationException("Username is required.");
        }

        if (requireSecret && string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new CheckmkOptionsValidationException("Secret is required for a new configuration.");
        }
    }

    public static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new CheckmkOptionsValidationException(
                "BaseUrl must be an absolute http or https URL without credentials.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new CheckmkOptionsValidationException("BaseUrl must not contain a username or password.");
        }

        RejectApiPathInBaseUrl(uri);
        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static bool TryValidate(CheckmkOptions options, out string? error)
    {
        try
        {
            Validate(options);
            error = null;
            return true;
        }
        catch (CheckmkOptionsValidationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void RejectApiPathInBaseUrl(Uri uri)
    {
        var path = uri.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return;
        }

        if (path.Contains("check_mk", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/api/", StringComparison.OrdinalIgnoreCase))
        {
            throw new CheckmkOptionsValidationException(
                "BaseUrl must be the server origin only (no /<site>/check_mk/api/1.0/ path).");
        }

        throw new CheckmkOptionsValidationException(
            "BaseUrl must be the server origin only, without a site or API path.");
    }
}
