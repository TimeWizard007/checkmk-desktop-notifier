namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public sealed class UserSettings
{
    public string? BaseUrl { get; init; }

    public string? Site { get; init; }

    public string? Username { get; init; }

    public int PollIntervalSeconds { get; init; } = CheckmkOptions.DefaultPollIntervalSeconds;

    public CheckmkOptions ToOptions(string? secret) =>
        new()
        {
            Mode = ClientMode.Real,
            BaseUrl = BaseUrl,
            Site = Site,
            Username = Username,
            Secret = secret,
            PollIntervalSeconds = PollIntervalSeconds <= 0
                ? CheckmkOptions.DefaultPollIntervalSeconds
                : PollIntervalSeconds
        };

    public static UserSettings FromOptions(CheckmkOptions options) =>
        new()
        {
            BaseUrl = options.BaseUrl,
            Site = options.Site,
            Username = options.Username,
            PollIntervalSeconds = options.PollIntervalSeconds
        };
}
