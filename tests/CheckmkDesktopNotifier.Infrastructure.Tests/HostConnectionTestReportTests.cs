using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class HostConnectionTestReportTests
{
    [Fact]
    public void Prints_counts_and_field_names_without_plugin_output_or_secrets()
    {
        var inspection = HostCollectionInspector.Inspect(FixtureReader.Read("host-collection-status.json"));
        var probe = new HostCollectionProbeResult
        {
            HttpStatusCode = 200,
            IsSuccess = true,
            Inspection = inspection
        };

        var report = HostConnectionTestReport.Format(probe);

        Assert.Contains("HTTP status: 200", report, StringComparison.Ordinal);
        Assert.Contains("Host objects: 6", report, StringComparison.Ordinal);
        Assert.Contains("UP: 1", report, StringComparison.Ordinal);
        Assert.Contains("DOWN: 3", report, StringComparison.Ordinal);
        Assert.Contains("UNREACHABLE: 2", report, StringComparison.Ordinal);
        Assert.Contains("state", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestOptions.Secret, report, StringComparison.Ordinal);
        Assert.DoesNotContain("Ping failed", report, StringComparison.Ordinal);
        Assert.DoesNotContain("routing incomplete", report, StringComparison.Ordinal);
        Assert.DoesNotContain("acknowledged outage", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Name_only_report_marks_state_counts_unavailable()
    {
        var inspection = HostCollectionInspector.Inspect(FixtureReader.Read("host-collection-name-only.json"));
        var probe = new HostCollectionProbeResult
        {
            HttpStatusCode = 200,
            IsSuccess = true,
            Inspection = inspection
        };

        var report = HostConnectionTestReport.Format(probe);

        Assert.Contains("UP: n/a", report, StringComparison.Ordinal);
        Assert.Contains("DOWN: n/a", report, StringComparison.Ordinal);
        Assert.Contains("UNREACHABLE: n/a", report, StringComparison.Ordinal);
        Assert.Contains("Next runtime test:", report, StringComparison.Ordinal);
        Assert.DoesNotContain("host-a", report, StringComparison.Ordinal);
        Assert.DoesNotContain("host-b", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Failed_probe_prints_status_without_inventing_counts()
    {
        var probe = new HostCollectionProbeResult
        {
            HttpStatusCode = 500,
            IsSuccess = false,
            ErrorKind = SnapshotErrorKind.Unavailable,
            ErrorMessage = "Checkmk is unavailable (HTTP 500)."
        };

        var report = HostConnectionTestReport.Format(probe);

        Assert.Contains("HTTP status: 500", report, StringComparison.Ordinal);
        Assert.Contains("Host objects: n/a", report, StringComparison.Ordinal);
        Assert.Contains("UP: n/a", report, StringComparison.Ordinal);
    }
}
