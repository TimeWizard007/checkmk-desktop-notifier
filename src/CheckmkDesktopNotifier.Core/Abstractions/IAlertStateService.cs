using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Abstractions;

public interface IAlertStateService
{
    AlertDelta ApplySnapshot(ProblemSnapshot snapshot);

    void MarkSeen(MonitoredObjectId id);

    void MarkAllNewAsSeen();

    IReadOnlyList<OpenIncident> GetOpenIncidents();

    DateTimeOffset? LastSuccessfulPollUtc { get; }
}
