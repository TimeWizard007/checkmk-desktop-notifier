using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class ConnectionTestReportTests
{
    [Fact]
    public void Prints_only_status_and_severity_counts()
    {
        var json = FixtureReader.Read("service-collection.json");
        var problems = ServiceProblemMapper.MapCollection(json, TestOptions.Site);
        var snapshot = ProblemSnapshot.Success(DateTimeOffset.UnixEpoch, TestOptions.Site, problems);

        var report = ConnectionTestReport.Format(200, snapshot);

        Assert.Contains("HTTP status: 200", report, StringComparison.Ordinal);
        Assert.Contains("Service problems: 4", report, StringComparison.Ordinal);
        Assert.Contains("WARN: 1", report, StringComparison.Ordinal);
        Assert.Contains("CRIT: 2", report, StringComparison.Ordinal);
        Assert.Contains("UNKNOWN: 1", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestOptions.Secret, report, StringComparison.Ordinal);
        Assert.DoesNotContain("plugin_output", report, StringComparison.Ordinal);
        Assert.DoesNotContain("load average", report, StringComparison.Ordinal);
        Assert.DoesNotContain("RAM usage", report, StringComparison.Ordinal);
        Assert.DoesNotContain("host-a", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Failed_snapshot_prints_zero_counts()
    {
        var snapshot = ProblemSnapshot.Failure(
            DateTimeOffset.UnixEpoch,
            SnapshotErrorKind.Authentication,
            "Checkmk authentication failed (HTTP 401).");

        var report = ConnectionTestReport.Format(401, snapshot);

        Assert.Contains("HTTP status: 401", report, StringComparison.Ordinal);
        Assert.Contains("Service problems: 0", report, StringComparison.Ordinal);
        Assert.Contains("WARN: 0", report, StringComparison.Ordinal);
        Assert.Contains("CRIT: 0", report, StringComparison.Ordinal);
        Assert.Contains("UNKNOWN: 0", report, StringComparison.Ordinal);
    }
}
