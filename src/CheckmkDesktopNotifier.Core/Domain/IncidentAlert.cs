namespace CheckmkDesktopNotifier.Core.Domain;

/// <summary>
/// UI-ready payload for one NEW incident notification. Built from Core incident state, not REST DTOs.
/// </summary>
public sealed record IncidentAlert
{
    public required MonitoredObjectId ObjectId { get; init; }

    public required Severity Severity { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>
    /// True when this balloon represents a host DOWN/UNREACHABLE group rather than a single service.
    /// Grouping never changes Core incident identities or Seen state.
    /// </summary>
    public bool IsGroupedHostFailure { get; init; }
}
