namespace CheckmkDesktopNotifier.Core.Domain;

/// <summary>
/// Current Checkmk observation for one host or service. Contains no local Seen state.
/// </summary>
public sealed record MonitoredProblem
{
    public required MonitoredObjectId Id { get; init; }
    public required Severity Severity { get; init; }
    public required StateType StateType { get; init; }
    public string? PluginOutput { get; init; }
    public DateTimeOffset? LastStateChange { get; init; }
    public DateTimeOffset? LastHardStateChange { get; init; }

    /// <summary>Service recurrence marker (<c>last_time_ok</c>).</summary>
    public DateTimeOffset? LastTimeOk { get; init; }

    /// <summary>Host recurrence marker (<c>last_time_up</c>).</summary>
    public DateTimeOffset? LastTimeUp { get; init; }

    public bool IsAcknowledgedInCheckmk { get; init; }
    public int ScheduledDowntimeDepth { get; init; }

    public DateTimeOffset? RecurrenceMarker =>
        Id.Kind == ObjectKind.Host ? LastTimeUp : LastTimeOk;
}
