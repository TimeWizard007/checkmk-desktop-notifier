using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Polling;

namespace CheckmkDesktopNotifier.App.MacOS;

public enum MacMenuBarConnectionState
{
    NotConfigured = 0,
    Disconnected = 1,
    Connected = 2,
    Error = 3
}

public readonly record struct MacMenuBarCounts(int New, int Critical, int Warning, int Unknown, int Taken);

public static class MacMenuBarStatus
{
    public const int MaxTitleLength = 28;

    public static MacMenuBarCounts FromIncidents(IReadOnlyList<OpenIncident> incidents)
    {
        ArgumentNullException.ThrowIfNull(incidents);
        var newest = 0;
        var critical = 0;
        var warning = 0;
        var unknown = 0;
        var taken = 0;
        foreach (var incident in incidents)
        {
            if (!incident.IsSeen)
            {
                newest++;
            }

            switch (incident.Severity)
            {
                case Severity.Critical:
                    critical++;
                    break;
                case Severity.Warning:
                    warning++;
                    break;
                case Severity.Unknown:
                    unknown++;
                    break;
            }

            if (incident.IsTakenByNotifier)
            {
                taken++;
            }
        }

        return new MacMenuBarCounts(newest, critical, warning, unknown, taken);
    }

    public static MacMenuBarConnectionState FromSession(bool configured, ConnectionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (!configured)
        {
            return MacMenuBarConnectionState.NotConfigured;
        }

        return status.Kind switch
        {
            ConnectionStatusKind.Connected => MacMenuBarConnectionState.Connected,
            ConnectionStatusKind.Refreshing => MacMenuBarConnectionState.Connected,
            ConnectionStatusKind.Error => MacMenuBarConnectionState.Error,
            _ => MacMenuBarConnectionState.Disconnected
        };
    }

    public static string FormatTitle(MacMenuBarCounts counts, MacMenuBarConnectionState state)
    {
        if (state == MacMenuBarConnectionState.NotConfigured)
        {
            return "Checkmk";
        }

        if (state == MacMenuBarConnectionState.Disconnected)
        {
            return "Checkmk · —";
        }

        if (state == MacMenuBarConnectionState.Error)
        {
            var errorCounts = FormatCounts(counts, includeUnknown: true, includeTaken: false);
            return errorCounts.Length + 2 <= MaxTitleLength ? "! " + errorCounts : "! Checkmk";
        }

        return FitCounts(counts);
    }

    public static string FormatToolTip(MacMenuBarCounts counts, MacMenuBarConnectionState state)
    {
        var connection = state switch
        {
            MacMenuBarConnectionState.NotConfigured => "Not configured",
            MacMenuBarConnectionState.Disconnected => "Disconnected",
            MacMenuBarConnectionState.Error => "Connection error",
            _ => "Connected"
        };

        return connection
               + " · NEW "
               + counts.New
               + " · CRIT "
               + counts.Critical
               + " · WARN "
               + counts.Warning
               + " · UNK "
               + counts.Unknown
               + " · TAKEN "
               + counts.Taken;
    }

    public static string FormatConnectionLabel(MacMenuBarConnectionState state) =>
        state switch
        {
            MacMenuBarConnectionState.NotConfigured => "Not configured",
            MacMenuBarConnectionState.Disconnected => "Disconnected",
            MacMenuBarConnectionState.Error => "Connection error",
            _ => "Connected"
        };

    private static string FitCounts(MacMenuBarCounts counts)
    {
        var full = FormatCounts(counts, includeUnknown: true, includeTaken: true);
        if (full.Length <= MaxTitleLength)
        {
            return full;
        }

        var withoutTaken = FormatCounts(counts, includeUnknown: true, includeTaken: false);
        if (withoutTaken.Length <= MaxTitleLength)
        {
            return withoutTaken;
        }

        var compact = FormatCounts(counts, includeUnknown: false, includeTaken: false);
        return compact.Length <= MaxTitleLength ? compact : "N:" + counts.New + " C:" + counts.Critical;
    }

    private static string FormatCounts(MacMenuBarCounts counts, bool includeUnknown, bool includeTaken)
    {
        var text = "N:" + counts.New + " C:" + counts.Critical + " W:" + counts.Warning;
        if (includeUnknown)
        {
            text += " U:" + counts.Unknown;
        }

        if (includeTaken)
        {
            text += " T:" + counts.Taken;
        }

        return text;
    }
}
