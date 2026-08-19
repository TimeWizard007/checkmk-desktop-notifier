using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Polling;

namespace CheckmkDesktopNotifier.App.MacOS;

public static class MacPollSummary
{
    public static string Format(IReadOnlyList<OpenIncident> incidents, ConnectionStatus status)
    {
        ArgumentNullException.ThrowIfNull(incidents);
        ArgumentNullException.ThrowIfNull(status);

        var hosts = 0;
        var services = 0;
        foreach (var incident in incidents)
        {
            if (incident.ObjectId.Kind == ObjectKind.Host)
            {
                hosts++;
            }
            else
            {
                services++;
            }
        }

        var headline = status.Kind switch
        {
            ConnectionStatusKind.Connected => "Connected",
            ConnectionStatusKind.Refreshing => "Refreshing...",
            ConnectionStatusKind.Error => "Connection error",
            _ => "Idle"
        };

        var last = status.LastSuccessfulPollUtc is { } utc
            ? utc.UtcDateTime.ToString("u")
            : "never";

        return headline
               + Environment.NewLine
               + "Problems: " + incidents.Count
               + Environment.NewLine
               + "Hosts: " + hosts
               + Environment.NewLine
               + "Services: " + services
               + Environment.NewLine
               + "Last poll: " + last;
    }
}
