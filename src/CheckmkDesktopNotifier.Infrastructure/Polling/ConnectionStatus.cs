namespace CheckmkDesktopNotifier.Infrastructure.Polling;

public enum ConnectionStatusKind
{
    Idle = 0,
    Refreshing = 1,
    Connected = 2,
    Error = 3
}

public sealed class ConnectionStatus
{
    public static ConnectionStatus Idle { get; } = new(ConnectionStatusKind.Idle, null, null);

    public ConnectionStatusKind Kind { get; }

    public DateTimeOffset? LastSuccessfulPollUtc { get; }

    public string? ErrorSummary { get; }

    public ConnectionStatus(
        ConnectionStatusKind kind,
        DateTimeOffset? lastSuccessfulPollUtc,
        string? errorSummary)
    {
        Kind = kind;
        LastSuccessfulPollUtc = lastSuccessfulPollUtc;
        ErrorSummary = Sanitize(errorSummary);
    }

    private static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (message.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            return "The Checkmk request failed.";
        }

        return message.Trim();
    }
}
