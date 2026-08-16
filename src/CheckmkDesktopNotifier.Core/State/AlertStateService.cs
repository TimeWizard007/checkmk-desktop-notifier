using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.State;

public sealed class AlertStateService : IAlertStateService
{
    internal const int MaxPersistedSummaryLength = 512;

    private IAlertStateStore _store;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<MonitoredObjectId, OpenIncident> _open;
    private DateTimeOffset? _lastSuccessfulPollUtc;

    public AlertStateService(IAlertStateStore store, TimeProvider? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? TimeProvider.System;

        var loaded = _store.Load();
        _open = loaded?.Incidents.ToDictionary(incident => incident.ObjectId)
                ?? new Dictionary<MonitoredObjectId, OpenIncident>();
        _lastSuccessfulPollUtc = loaded?.LastSuccessfulPollUtc;
    }

    public AlertDelta ApplySnapshot(ProblemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            if (!snapshot.IsSuccess)
            {
                return AlertDelta.Empty;
            }

            var now = _clock.GetUtcNow();
            var current = new Dictionary<MonitoredObjectId, MonitoredProblem>();
            foreach (var problem in snapshot.Problems)
            {
                if (problem.StateType != StateType.Hard)
                {
                    continue;
                }

                current[problem.Id] = problem;
            }

            var opened = new List<OpenIncident>();
            var recovered = new List<RecoveredIncident>();
            var severityChanged = new List<OpenIncident>();

            foreach (var (id, incident) in _open.ToArray())
            {
                if (current.ContainsKey(id))
                {
                    continue;
                }

                recovered.Add(ToRecovered(incident, now));
                _open.Remove(id);
            }

            foreach (var (id, problem) in current)
            {
                if (_open.TryGetValue(id, out var existing)
                    && IsRecurrence(existing.BoundRecurrenceMarker, problem.RecurrenceMarker))
                {
                    recovered.Add(ToRecovered(existing, now));
                    var replacement = CreateIncident(problem, now);
                    _open[id] = replacement;
                    opened.Add(replacement);
                    continue;
                }

                if (existing is not null)
                {
                    var updated = existing with
                    {
                        Severity = problem.Severity,
                        LastObservedAtUtc = now,
                        LastSummary = TruncateSummary(problem.PluginOutput),
                        IsAcknowledgedInCheckmk = problem.IsAcknowledgedInCheckmk,
                        ScheduledDowntimeDepth = problem.ScheduledDowntimeDepth
                    };

                    _open[id] = updated;
                    if (updated.Severity != existing.Severity)
                    {
                        severityChanged.Add(updated);
                    }

                    continue;
                }

                var created = CreateIncident(problem, now);
                _open[id] = created;
                opened.Add(created);
            }

            _lastSuccessfulPollUtc = snapshot.RetrievedAt;
            PersistUnlocked();
            return new AlertDelta(opened, recovered, severityChanged);
        }
    }

    public void MarkSeen(MonitoredObjectId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (_gate)
        {
            if (!_open.TryGetValue(id, out var incident) || incident.IsSeen)
            {
                return;
            }

            _open[id] = incident with { IsSeen = true };
            PersistUnlocked();
        }
    }

    public void MarkAllNewAsSeen()
    {
        lock (_gate)
        {
            var changed = false;
            foreach (var (id, incident) in _open.ToArray())
            {
                if (incident.IsSeen)
                {
                    continue;
                }

                _open[id] = incident with { IsSeen = true };
                changed = true;
            }

            if (changed)
            {
                PersistUnlocked();
            }
        }
    }

    public IReadOnlyList<OpenIncident> GetOpenIncidents()
    {
        lock (_gate)
        {
            return _open.Values
                .OrderBy(incident => incident.IsSeen)
                .ThenByDescending(incident => incident.OpenedAtUtc)
                .ThenBy(incident => incident.ObjectId.HostName, StringComparer.Ordinal)
                .ThenBy(incident => incident.ObjectId.ServiceDescription, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public DateTimeOffset? LastSuccessfulPollUtc
    {
        get
        {
            lock (_gate)
            {
                return _lastSuccessfulPollUtc;
            }
        }
    }

    public void ReplaceStore(IAlertStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        lock (_gate)
        {
            _store = store;
            var loaded = _store.Load();
            _open.Clear();
            if (loaded is not null)
            {
                foreach (var incident in loaded.Incidents)
                {
                    _open[incident.ObjectId] = incident;
                }
            }

            _lastSuccessfulPollUtc = loaded?.LastSuccessfulPollUtc;
        }
    }

    internal static bool IsUsableRecurrenceMarker(DateTimeOffset? value) =>
        value is { } timestamp && timestamp > DateTimeOffset.UnixEpoch;

    internal static bool IsRecurrence(DateTimeOffset? boundMarker, DateTimeOffset? currentMarker) =>
        IsUsableRecurrenceMarker(boundMarker)
        && IsUsableRecurrenceMarker(currentMarker)
        && currentMarker > boundMarker;

    private static OpenIncident CreateIncident(MonitoredProblem problem, DateTimeOffset now) =>
        new()
        {
            ObjectId = problem.Id,
            Severity = problem.Severity,
            IsSeen = false,
            OpenedAtUtc = now,
            LastObservedAtUtc = now,
            BoundRecurrenceMarker = IsUsableRecurrenceMarker(problem.RecurrenceMarker)
                ? problem.RecurrenceMarker
                : null,
            LastSummary = TruncateSummary(problem.PluginOutput),
            IsAcknowledgedInCheckmk = problem.IsAcknowledgedInCheckmk,
            ScheduledDowntimeDepth = problem.ScheduledDowntimeDepth
        };

    private static RecoveredIncident ToRecovered(OpenIncident incident, DateTimeOffset recoveredAtUtc) =>
        new()
        {
            ObjectId = incident.ObjectId,
            LastSeverity = incident.Severity,
            WasSeen = incident.IsSeen,
            OpenedAtUtc = incident.OpenedAtUtc,
            RecoveredAtUtc = recoveredAtUtc
        };

    private static string? TruncateSummary(string? pluginOutput)
    {
        if (string.IsNullOrWhiteSpace(pluginOutput))
        {
            return null;
        }

        var trimmed = pluginOutput.Trim();
        return trimmed.Length <= MaxPersistedSummaryLength
            ? trimmed
            : trimmed[..MaxPersistedSummaryLength];
    }

    private void PersistUnlocked()
    {
        _store.Save(new AlertStateDocument
        {
            SchemaVersion = AlertStateDocument.CurrentSchemaVersion,
            LastSuccessfulPollUtc = _lastSuccessfulPollUtc,
            Incidents = _open.Values.ToArray()
        });
    }
}
