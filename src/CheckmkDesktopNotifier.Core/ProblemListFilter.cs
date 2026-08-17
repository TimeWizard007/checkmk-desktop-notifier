using CheckmkDesktopNotifier.Core.Domain;

namespace CheckmkDesktopNotifier.Core;

public enum ProblemListFilter
{
    All = 0,
    New = 1,
    Critical = 2,
    Warning = 3,
    Unknown = 4
}

public static class ProblemListFilterLogic
{
    public static IReadOnlyList<OpenIncident> Apply(
        IReadOnlyList<OpenIncident> incidents,
        ProblemListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(incidents);

        return filter switch
        {
            ProblemListFilter.New => incidents.Where(incident => !incident.IsSeen).ToArray(),
            ProblemListFilter.Critical => incidents.Where(incident => incident.Severity == Severity.Critical).ToArray(),
            ProblemListFilter.Warning => incidents.Where(incident => incident.Severity == Severity.Warning).ToArray(),
            ProblemListFilter.Unknown => incidents.Where(incident => incident.Severity == Severity.Unknown).ToArray(),
            _ => incidents.ToArray()
        };
    }
}

/// <summary>
/// Presentation-only list filter. Does not mutate incident state, Seen, or notifications.
/// </summary>
public sealed class ProblemListViewState
{
    public ProblemListFilter ActiveFilter { get; private set; } = ProblemListFilter.All;

    public bool IsExpanded { get; private set; }

    public void OpenFilter(ProblemListFilter filter)
    {
        ActiveFilter = filter;
        IsExpanded = true;
    }

    /// <summary>
    /// Compact-bar counter: open on this filter, switch if another filter is showing,
    /// or close when the same filter is already active. Does not create a window.
    /// </summary>
    public void ToggleCounter(ProblemListFilter filter)
    {
        if (IsExpanded && ActiveFilter == filter)
        {
            IsExpanded = false;
            return;
        }

        ActiveFilter = filter;
        IsExpanded = true;
    }

    public void ToggleFromBarBackground()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            return;
        }

        ActiveFilter = ProblemListFilter.All;
        IsExpanded = true;
    }

    public void Collapse() => IsExpanded = false;
}
