using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Mock;

/// <summary>
/// Realistic in-memory monitoring scenario for the Phase 2 UI. No HTTP.
/// </summary>
public static class DemoSnapshotFactory
{
    public static readonly SiteId SiteId = new("itssrv");

    public static MonitoredObjectId CpuCriticalId { get; } =
        MonitoredObjectId.Service(SiteId, "SRV-SQL01", "CPU utilization");

    public static MonitoredObjectId WebHostDownId { get; } =
        MonitoredObjectId.Host(SiteId, "SRV-WEB02");

    public static MonitoredObjectId DiskWarningId { get; } =
        MonitoredObjectId.Service(SiteId, "SRV-APP01", "Filesystem /var");

    public static MonitoredObjectId MemorySeenId { get; } =
        MonitoredObjectId.Service(SiteId, "SRV-APP01", "Memory");

    public static MonitoredObjectId ImapUnknownId { get; } =
        MonitoredObjectId.Service(SiteId, "SRV-MAIL01", "IMAP");

    public static MonitoredObjectId MssqlAckedId { get; } =
        MonitoredObjectId.Service(SiteId, "SRV-SQL01", "MSSQL Health");

    public static MonitoredObjectId BackupDowntimeId { get; } =
        MonitoredObjectId.Service(SiteId, "SRV-BACKUP01", "Backup job");

    /// <summary>Local Seen is applied by the UI bootstrapper, not by Checkmk.</summary>
    public static MonitoredObjectId IncidentToMarkSeen => MemorySeenId;

    public static ProblemSnapshot Create(DateTimeOffset retrievedAt)
    {
        var lastOk = retrievedAt.AddHours(-3);
        var lastUp = retrievedAt.AddHours(-1);

        return ProblemSnapshot.Success(retrievedAt, SiteId,
        [
            new MonitoredProblem
            {
                Id = CpuCriticalId,
                Severity = Severity.Critical,
                StateType = StateType.Hard,
                PluginOutput = "CPU utilization 97.4% (warn/crit at 80.00/95.00)",
                LastTimeOk = lastOk
            },
            new MonitoredProblem
            {
                Id = WebHostDownId,
                Severity = Severity.Critical,
                StateType = StateType.Hard,
                PluginOutput = "PING CRITICAL - Host Unreachable (192.0.2.22)",
                LastTimeUp = lastUp
            },
            new MonitoredProblem
            {
                Id = DiskWarningId,
                Severity = Severity.Warning,
                StateType = StateType.Hard,
                PluginOutput = "Used 88.1% of 50.0 GiB (warn/crit at 80.00%/90.00%)",
                LastTimeOk = lastOk.AddHours(-2)
            },
            new MonitoredProblem
            {
                Id = MemorySeenId,
                Severity = Severity.Warning,
                StateType = StateType.Hard,
                PluginOutput = "RAM used 82.0% of 32.0 GiB",
                LastTimeOk = lastOk.AddDays(-1)
            },
            new MonitoredProblem
            {
                Id = ImapUnknownId,
                Severity = Severity.Unknown,
                StateType = StateType.Hard,
                PluginOutput = "Agent returned no data for IMAP",
                LastTimeOk = lastOk.AddMinutes(-40)
            },
            new MonitoredProblem
            {
                Id = MssqlAckedId,
                Severity = Severity.Critical,
                StateType = StateType.Hard,
                PluginOutput = "Login timeout on instance MSSQLSERVER",
                LastTimeOk = lastOk.AddHours(-6),
                IsAcknowledgedInCheckmk = true
            },
            new MonitoredProblem
            {
                Id = BackupDowntimeId,
                Severity = Severity.Warning,
                StateType = StateType.Hard,
                PluginOutput = "Last successful backup 26 hours ago",
                LastTimeOk = lastOk.AddHours(-20),
                ScheduledDowntimeDepth = 1
            }
        ]);
    }
}
