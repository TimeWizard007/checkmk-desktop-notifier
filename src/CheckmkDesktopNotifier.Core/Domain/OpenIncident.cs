namespace CheckmkDesktopNotifier.Core.Domain;

/// <summary>
/// Locally tracked uninterrupted non-OK period for one monitored object.
/// </summary>
public sealed record OpenIncident
{
    public required MonitoredObjectId ObjectId { get; init; }
    public required Severity Severity { get; init; }
    public required bool IsSeen { get; init; }
    public required DateTimeOffset OpenedAtUtc { get; init; }
    public required DateTimeOffset LastObservedAtUtc { get; init; }

    /// <summary>
    /// Recurrence marker captured when this local incident was opened
    /// (<c>last_time_ok</c> for services, <c>last_time_up</c> for hosts).
    /// </summary>
    public DateTimeOffset? BoundRecurrenceMarker { get; init; }

    public string? LastSummary { get; init; }
    public bool IsAcknowledgedInCheckmk { get; init; }
    public int ScheduledDowntimeDepth { get; init; }

    public IncidentStatus Status => IsSeen ? IncidentStatus.Seen : IncidentStatus.New;
}
