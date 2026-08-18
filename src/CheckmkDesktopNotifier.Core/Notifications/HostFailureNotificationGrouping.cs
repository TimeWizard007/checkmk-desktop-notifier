using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core.Notifications;

/// <summary>
/// Notification-only coalescing for HARD host DOWN/UNREACHABLE.
/// Does not merge incident identities, hide child services, or mark Seen.
/// </summary>
public static class HostFailureNotificationGrouping
{
    public readonly record struct HostKey(string SiteId, string HostName);

    public static HostKey KeyOf(MonitoredObjectId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return new HostKey(id.SiteId.Value, id.HostName);
    }

    public static bool IsGroupingHost(MonitoredProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return problem.Id.Kind == ObjectKind.Host
            && problem.StateType == StateType.Hard
            && problem.Severity is Severity.Critical or Severity.Unknown;
    }

    public static HashSet<HostKey> GroupingHosts(ProblemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var keys = new HashSet<HostKey>();
        if (!snapshot.IsSuccess)
        {
            return keys;
        }

        foreach (var problem in snapshot.Problems)
        {
            if (IsGroupingHost(problem))
            {
                keys.Add(KeyOf(problem.Id));
            }
        }

        return keys;
    }

    /// <summary>
    /// Count of active non-OK service problems for this host in the merged snapshot
    /// (the same HARD problems the UI lists). Not REST <c>num_services_hard_*</c>.
    /// </summary>
    public static int CountAffectedServices(ProblemSnapshot snapshot, HostKey host)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.IsSuccess)
        {
            return 0;
        }

        var count = 0;
        foreach (var problem in snapshot.Problems)
        {
            if (problem.Id.Kind == ObjectKind.Service && KeyOf(problem.Id).Equals(host))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Alerts to emit for this successful snapshot. Failed snapshots must not call this
    /// (the coordinator returns first). Deterministic: no wall-clock grouping window.
    /// </summary>
    public static IReadOnlyList<IncidentAlert> SelectAlerts(ProblemSnapshot snapshot, AlertDelta delta)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(delta);

        if (!snapshot.IsSuccess)
        {
            return Array.Empty<IncidentAlert>();
        }

        var groupingHosts = GroupingHosts(snapshot);
        var alerts = new List<IncidentAlert>();
        foreach (var opened in delta.Opened)
        {
            if (opened.IsSeen || opened.IsAcknowledgedInCheckmk)
            {
                continue;
            }

            try
            {
                var key = KeyOf(opened.ObjectId);
                if (opened.ObjectId.Kind == ObjectKind.Host && groupingHosts.Contains(key))
                {
                    alerts.Add(IncidentAlertFormatter.FromGroupedHost(
                        opened,
                        CountAffectedServices(snapshot, key)));
                    continue;
                }

                if (opened.ObjectId.Kind == ObjectKind.Service && groupingHosts.Contains(key))
                {
                    continue;
                }

                alerts.Add(IncidentAlertFormatter.From(opened));
            }
            catch (Exception)
            {
            }
        }

        return alerts;
    }
}
