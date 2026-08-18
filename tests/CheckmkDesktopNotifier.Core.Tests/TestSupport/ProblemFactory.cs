using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Tests.TestSupport;

internal static class ProblemFactory
{
    public static readonly SiteId DefaultSite = new("itssrv");

    public static readonly DateTimeOffset T0 = new(2026, 8, 15, 18, 0, 0, TimeSpan.Zero);

    public static MonitoredObjectId ServiceId(string hostName, string serviceDescription) =>
        MonitoredObjectId.Service(DefaultSite, hostName, serviceDescription);

    public static MonitoredObjectId HostId(string hostName) =>
        MonitoredObjectId.Host(DefaultSite, hostName);

        public static MonitoredProblem Service(
        string hostName,
        string serviceDescription,
        Severity severity,
        StateType stateType = StateType.Hard,
        DateTimeOffset? lastTimeOk = null,
        string? pluginOutput = null,
        bool acknowledged = false,
        int downtimeDepth = 0,
        AcknowledgementType acknowledgementType = AcknowledgementType.None,
        string? takenBy = null,
        bool takenByNotifier = false) =>
        new()
        {
            Id = ServiceId(hostName, serviceDescription),
            Severity = severity,
            StateType = stateType,
            PluginOutput = pluginOutput,
            LastTimeOk = lastTimeOk,
            IsAcknowledgedInCheckmk = acknowledged,
            AcknowledgementType = acknowledgementType,
            TakenByDisplayName = takenBy,
            IsTakenByNotifier = takenByNotifier,
            ScheduledDowntimeDepth = downtimeDepth
        };

    public static MonitoredProblem Host(
        string hostName,
        Severity severity,
        StateType stateType = StateType.Hard,
        DateTimeOffset? lastTimeUp = null,
        string? pluginOutput = null,
        bool acknowledged = false,
        AcknowledgementType acknowledgementType = AcknowledgementType.None,
        string? takenBy = null,
        bool takenByNotifier = false) =>
        new()
        {
            Id = HostId(hostName),
            Severity = severity,
            StateType = stateType,
            PluginOutput = pluginOutput,
            LastTimeUp = lastTimeUp,
            IsAcknowledgedInCheckmk = acknowledged,
            AcknowledgementType = acknowledgementType,
            TakenByDisplayName = takenBy,
            IsTakenByNotifier = takenByNotifier
        };

    public static ProblemSnapshot Ok(DateTimeOffset retrievedAt, params MonitoredProblem[] problems) =>
        ProblemSnapshot.Success(retrievedAt, DefaultSite, problems);

    public static ProblemSnapshot Failed(DateTimeOffset retrievedAt) =>
        ProblemSnapshot.Failure(retrievedAt, SnapshotErrorKind.Unavailable, "Checkmk unreachable");
}
