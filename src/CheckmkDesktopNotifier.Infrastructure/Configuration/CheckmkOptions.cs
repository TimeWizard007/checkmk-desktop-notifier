namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public sealed class CheckmkOptions
{
    public const int DefaultPollIntervalSeconds = 60;
    public const int MinimumPollIntervalSeconds = 10;
    public const int MinimumHttpTimeoutSeconds = 5;

    public ClientMode Mode { get; init; } = ClientMode.Mock;

    public string? BaseUrl { get; init; }

    public string? Site { get; init; }

    public string? Username { get; init; }

    public string? Secret { get; init; }

    public int PollIntervalSeconds { get; init; } = DefaultPollIntervalSeconds;

    public Uri CreateApiBaseUri()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrWhiteSpace(Site))
        {
            throw new InvalidOperationException("BaseUrl and Site are required to build the Checkmk API URI.");
        }

        var root = BaseUrl.Trim().TrimEnd('/');
        var site = Site.Trim().Trim('/');
        return new Uri($"{root}/{site}/check_mk/api/1.0/", UriKind.Absolute);
    }

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public TimeSpan CreateHttpTimeout()
    {
        var interval = Math.Max(MinimumPollIntervalSeconds, PollIntervalSeconds);
        var timeoutSeconds = Math.Max(MinimumHttpTimeoutSeconds, interval - 2);
        if (timeoutSeconds >= interval)
        {
            timeoutSeconds = Math.Max(1, interval - 1);
        }

        return TimeSpan.FromSeconds(timeoutSeconds);
    }
}
