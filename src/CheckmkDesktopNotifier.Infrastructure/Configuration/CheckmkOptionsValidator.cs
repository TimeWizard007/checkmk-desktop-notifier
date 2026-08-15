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
}
