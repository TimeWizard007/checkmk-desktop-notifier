using System.Text;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Notifications;

/// <summary>
/// Builds concise notification text from an open incident. Truncates plugin output; does not include secrets.
/// Length limits match unpackaged WinForms balloon-tip constraints.
/// </summary>
public static class IncidentAlertFormatter
{
    public const int MaxTitleLength = 63;
    public const int MaxBodyLength = 255;
    public const int MaxSummaryLength = 120;

    public static IncidentAlert From(OpenIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        return new IncidentAlert
        {
            ObjectId = incident.ObjectId,
            Severity = incident.Severity,
            Title = Truncate(ProductInfo.ProductName, MaxTitleLength),
            Body = FormatBody(incident)
        };
    }

    public static IncidentAlert FromGroupedHost(OpenIncident hostIncident, int affectedServiceCount)
    {
        ArgumentNullException.ThrowIfNull(hostIncident);
        if (hostIncident.ObjectId.Kind != ObjectKind.Host)
        {
            throw new ArgumentException("Grouped host alerts require a host incident.", nameof(hostIncident));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(affectedServiceCount);

        var countLine = affectedServiceCount == 1
            ? "1 affected service"
            : $"{affectedServiceCount} affected services";
        var body = string.Join(
            "\n",
            SeverityHeadline(hostIncident),
            hostIncident.ObjectId.HostName,
            countLine);

        return new IncidentAlert
        {
            ObjectId = hostIncident.ObjectId,
            Severity = hostIncident.Severity,
            Title = Truncate(ProductInfo.ProductName, MaxTitleLength),
            Body = Truncate(body, MaxBodyLength),
            IsGroupedHostFailure = true
        };
    }

    public static string SeverityHeadline(OpenIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        if (incident.ObjectId.Kind == ObjectKind.Host)
        {
            return incident.Severity switch
            {
                Severity.Critical => "HOST DOWN",
                Severity.Unknown => "HOST UNREACHABLE",
                Severity.Warning => "WARNING",
                _ => incident.Severity.ToString().ToUpperInvariant()
            };
        }

        return incident.Severity switch
        {
            Severity.Critical => "CRITICAL",
            Severity.Warning => "WARNING",
            Severity.Unknown => "UNKNOWN",
            _ => incident.Severity.ToString().ToUpperInvariant()
        };
    }

    private static string FormatBody(OpenIncident incident)
    {
        var lines = new List<string> { SeverityHeadline(incident), incident.ObjectId.HostName };
        if (incident.ObjectId.Kind == ObjectKind.Service
            && !string.IsNullOrWhiteSpace(incident.ObjectId.ServiceDescription))
        {
            lines.Add(incident.ObjectId.ServiceDescription);
        }

        var summary = SanitizeSummary(incident.LastSummary);
        if (!string.IsNullOrEmpty(summary))
        {
            lines.Add(summary);
        }

        var body = string.Join("\n", lines);
        return Truncate(body, MaxBodyLength);
    }

    private static string? SanitizeSummary(string? pluginOutput)
    {
        if (string.IsNullOrWhiteSpace(pluginOutput))
        {
            return null;
        }

        var builder = new StringBuilder(pluginOutput.Length);
        foreach (var ch in pluginOutput.Trim())
        {
            if (char.IsControl(ch) && ch is not '\t')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(ch);
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        return Truncate(cleaned, MaxSummaryLength);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (maxLength <= 0 || value.Length <= maxLength)
        {
            return value;
        }

        return maxLength == 1 ? value[..1] : value[..(maxLength - 1)] + "…";
    }
}
