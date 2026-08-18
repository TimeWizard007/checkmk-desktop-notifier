using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Abstractions;

public interface IAlertStateService
{
    AlertDelta ApplySnapshot(ProblemSnapshot snapshot);

    void MarkSeen(MonitoredObjectId id);

    void MarkUnseen(MonitoredObjectId id);

    void MarkAllNewAsSeen();

    IReadOnlyList<OpenIncident> GetOpenIncidents();

    DateTimeOffset? LastSuccessfulPollUtc { get; }

    /// <summary>
    /// Replaces the backing store and reloads in-memory state from it.
    /// Does not write the previous in-memory incidents into the new store.
    /// </summary>
    void ReplaceStore(IAlertStateStore store);
}
