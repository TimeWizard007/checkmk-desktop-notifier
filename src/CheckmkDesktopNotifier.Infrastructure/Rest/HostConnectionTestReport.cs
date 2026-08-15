using System.Text;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public static class HostConnectionTestReport
{
    public static string Format(HostCollectionProbeResult verified, HostCollectionProbeResult? documentedColumns = null)
    {
        ArgumentNullException.ThrowIfNull(verified);

        var builder = new StringBuilder();
        builder.AppendLine("Verified GET /domain-types/host/collections/all");
        AppendProbe(builder, verified);

        if (documentedColumns is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Documented columns GET (query-string columns= only; not used by the app)");
            AppendProbe(builder, documentedColumns);
        }

        return builder.ToString();
    }

    private static void AppendProbe(StringBuilder builder, HostCollectionProbeResult probe)
    {
        builder.AppendLine($"HTTP status: {probe.HttpStatusCode?.ToString() ?? "n/a"}");

        if (!probe.IsSuccess || probe.Inspection is null)
        {
            builder.AppendLine("Host objects: n/a");
            builder.AppendLine("UP: n/a");
            builder.AppendLine("DOWN: n/a");
            builder.AppendLine("UNREACHABLE: n/a");
            builder.AppendLine("Monitoring fields present: n/a");
            builder.AppendLine("Monitoring fields missing: n/a");
            if (!string.IsNullOrWhiteSpace(probe.ErrorMessage))
            {
                builder.AppendLine($"Result: failed ({probe.ErrorKind})");
            }

            return;
        }

        var inspection = probe.Inspection;
        builder.AppendLine($"Host objects: {inspection.HostCount}");
        builder.AppendLine($"UP: {FormatCount(inspection.UpCount)}");
        builder.AppendLine($"DOWN: {FormatCount(inspection.DownCount)}");
        builder.AppendLine($"UNREACHABLE: {FormatCount(inspection.UnreachableCount)}");
        builder.AppendLine($"Identity field: {inspection.IdentitySource}");
        builder.AppendLine(
            "Monitoring fields present: "
            + (inspection.PresentMonitoringFields.Count == 0
                ? "(none)"
                : string.Join(", ", inspection.PresentMonitoringFields)));
        builder.AppendLine(
            "Monitoring fields missing: "
            + (inspection.MissingMonitoringFields.Count == 0
                ? "(none)"
                : string.Join(", ", inspection.MissingMonitoringFields)));
        if (!string.IsNullOrWhiteSpace(inspection.NextRuntimeTest))
        {
            builder.AppendLine($"Next runtime test: {inspection.NextRuntimeTest}");
        }
    }

    private static string FormatCount(int? count) => count?.ToString() ?? "n/a";
}
