using CheckmkDesktopNotifier.Infrastructure.Rest;

namespace CheckmkDesktopNotifier.Platform.MacOS;

/// <summary>
/// Catches managed exceptions at the AppKit IMP boundary so they cannot terminate
/// the process. Logs through <see cref="ErrorSink"/>; never writes secrets.
/// Native SIGSEGV cannot be caught here — Intel NSRect queries must not use
/// <c>objc_msgSend</c>.
/// </summary>
public static class MacNativeCallbackGuard
{
    public static Action<Exception>? ErrorSink { get; set; }

    public static void Run(Action action, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Report(ex, onError);
        }
    }

    public static void Report(Exception exception, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var sink = onError ?? ErrorSink;
        if (sink is null)
        {
            return;
        }

        try
        {
            sink(exception);
        }
        catch (Exception)
        {
        }
    }

    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var type = exception.GetType().FullName ?? "Exception";
        return type + ": " + Sanitize(exception.Message);
    }

    public static string Sanitize(string? message)
    {
        var sanitized = ConnectionTestResult.Sanitize(message);
        if (ContainsSecret(sanitized))
        {
            return "The Checkmk request failed.";
        }

        return sanitized;
    }

    private static bool ContainsSecret(string message) =>
        message.Contains("password", StringComparison.OrdinalIgnoreCase)
        || message.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || message.Contains("token", StringComparison.OrdinalIgnoreCase)
        || message.Contains("Keychain", StringComparison.OrdinalIgnoreCase);
}
