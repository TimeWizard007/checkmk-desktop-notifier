namespace CheckmkDesktopNotifier.Core.Domain;

/// <summary>
/// Normalized acknowledgement metadata carried on a snapshot problem / open incident.
/// Raw Checkmk comments are not persisted.
/// </summary>
public sealed record CheckmkAcknowledgementInfo(
    bool IsAcknowledged,
    AcknowledgementType AcknowledgementType,
    string? TakenByDisplayName,
    bool IsTakenByNotifier)
{
    public static CheckmkAcknowledgementInfo None { get; } = new(
        IsAcknowledged: false,
        AcknowledgementType: AcknowledgementType.None,
        TakenByDisplayName: null,
        IsTakenByNotifier: false);
}
