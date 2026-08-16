using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public enum ConnectionTestStatus
{
    Success = 0,
    Unauthorized = 1,
    Forbidden = 2,
    Unreachable = 3,
    Timeout = 4,
    TlsError = 5,
    InvalidConfiguration = 6,
    UnexpectedApiResponse = 7,
    Unavailable = 8
}

public sealed class ConnectionTestResult
{
    public ConnectionTestStatus Status { get; init; }

    public bool ServicesReachable { get; init; }

    public bool HostsReachable { get; init; }

    public int? ServiceCount { get; init; }

    public int? HostCount { get; init; }

    public int? HttpStatus { get; init; }

    public string? UserMessage { get; init; }

    public static ConnectionTestResult FromStatus(
        ConnectionTestStatus status,
        string message,
        int? httpStatus = null,
        bool servicesReachable = false,
        bool hostsReachable = false,
        int? serviceCount = null,
        int? hostCount = null) =>
        new()
        {
            Status = status,
            UserMessage = Sanitize(message),
            HttpStatus = httpStatus,
            ServicesReachable = servicesReachable,
            HostsReachable = hostsReachable,
            ServiceCount = serviceCount,
            HostCount = hostCount
        };

    public static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "The Checkmk request failed.";
        }

        if (message.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            return "The Checkmk request failed.";
        }

        var firstLine = message.Replace("\r", " ", StringComparison.Ordinal)
            .Split('\n', 2, StringSplitOptions.None)[0]
            .Trim();
        return firstLine.Length <= 200 ? firstLine : firstLine[..200];
    }
}

public static class HttpFailureClassifier
{
    public static ConnectionTestStatus ClassifyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
        {
            return ConnectionTestStatus.Timeout;
        }

        if (exception is HttpRequestException http)
        {
            if (http.HttpRequestError == HttpRequestError.SecureConnectionError
                || http.InnerException is AuthenticationException)
            {
                return ConnectionTestStatus.TlsError;
            }

            if (http.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError
                || http.InnerException is SocketException)
            {
                return ConnectionTestStatus.Unreachable;
            }
        }

        return ConnectionTestStatus.Unreachable;
    }

    public static string UserMessage(ConnectionTestStatus status) =>
        status switch
        {
            ConnectionTestStatus.Timeout => "The Checkmk request timed out.",
            ConnectionTestStatus.TlsError => "TLS or certificate validation failed.",
            ConnectionTestStatus.Unreachable => "The Checkmk server cannot be reached.",
            ConnectionTestStatus.Unauthorized => "Authentication failed.",
            ConnectionTestStatus.Forbidden => "Access was forbidden.",
            ConnectionTestStatus.InvalidConfiguration => "The configuration is invalid.",
            ConnectionTestStatus.UnexpectedApiResponse => "The Checkmk API returned an unexpected response.",
            _ => "The Checkmk request failed."
        };
}
