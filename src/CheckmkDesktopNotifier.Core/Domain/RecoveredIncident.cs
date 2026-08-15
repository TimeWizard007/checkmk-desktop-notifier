namespace CheckmkDesktopNotifier.Core.Domain;

public sealed record RecoveredIncident
{
    public required MonitoredObjectId ObjectId { get; init; }
    public required Severity LastSeverity { get; init; }
    public required bool WasSeen { get; init; }
    public required DateTimeOffset OpenedAtUtc { get; init; }
    public required DateTimeOffset RecoveredAtUtc { get; init; }
}
