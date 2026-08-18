using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class ProblemListFilterTests
{
    private readonly MutableTimeProvider _clock = new(ProblemFactory.T0);

    [Fact]
    public void All_returns_all_open_incidents()
    {
        var alerts = Seed();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void New_returns_only_new_incidents()
    {
        var alerts = Seed();
        alerts.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.New);
        Assert.Equal(3, result.Count);
        Assert.All(result, incident => Assert.False(incident.IsSeen));
    }

    [Fact]
    public void Crit_returns_only_critical_including_seen()
    {
        var alerts = Seed();
        alerts.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Critical);
        Assert.Equal(2, result.Count);
        Assert.All(result, incident => Assert.Equal(Severity.Critical, incident.Severity));
        Assert.Contains(result, incident => incident.IsSeen);
    }

    [Fact]
    public void Warn_returns_only_warning()
    {
        var alerts = Seed();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Warning);
        var item = Assert.Single(result);
        Assert.Equal(Severity.Warning, item.Severity);
    }

    [Fact]
    public void Unk_returns_only_unknown()
    {
        var alerts = Seed();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Unknown);
        var item = Assert.Single(result);
        Assert.Equal(Severity.Unknown, item.Severity);
    }

    [Fact]
    public void Seen_incident_disappears_from_new_only()
    {
        var alerts = Seed();
        var cpu = ProblemFactory.ServiceId("web01", "CPU");
        alerts.MarkSeen(cpu);

        var newest = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.New);
        Assert.DoesNotContain(newest, incident => incident.ObjectId.Equals(cpu));

        var crit = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Critical);
        Assert.Contains(crit, incident => incident.ObjectId.Equals(cpu) && incident.IsSeen);
    }

    [Fact]
    public void Single_seen_updates_new_filter_only_for_that_incident()
    {
        var alerts = Seed();
        alerts.MarkSeen(ProblemFactory.ServiceId("web01", "CPU"));
        var newest = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.New);
        Assert.Contains(newest, incident => incident.ObjectId.Equals(ProblemFactory.ServiceId("web01", "Disk")));
        Assert.DoesNotContain(newest, incident => incident.ObjectId.Equals(ProblemFactory.ServiceId("web01", "CPU")));
    }

    [Fact]
    public void Polling_refresh_preserves_active_filter()
    {
        var state = new ProblemListViewState();
        state.OpenFilter(ProblemListFilter.Critical);
        var alerts = Seed();
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        alerts.ApplySnapshot(ProblemFactory.Ok(
            _clock.UtcNow,
            ProblemFactory.Service("web01", "CPU", Severity.Critical),
            ProblemFactory.Service("web01", "Mem", Severity.Critical),
            ProblemFactory.Service("web01", "Disk", Severity.Warning)));

        Assert.Equal(ProblemListFilter.Critical, state.ActiveFilter);
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), state.ActiveFilter);
        Assert.Equal(2, result.Count);
        Assert.All(result, incident => Assert.Equal(Severity.Critical, incident.Severity));
    }

    [Fact]
    public void Recovered_object_disappears_from_filtered_view()
    {
        var state = new ProblemListViewState();
        state.OpenFilter(ProblemListFilter.Warning);
        var alerts = Seed();
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        alerts.ApplySnapshot(ProblemFactory.Ok(
            _clock.UtcNow,
            ProblemFactory.Service("web01", "CPU", Severity.Critical),
            ProblemFactory.Service("edge", "Agent", Severity.Unknown)));

        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), state.ActiveFilter);
        Assert.Empty(result);
    }

    [Fact]
    public void Mark_all_new_as_seen_empties_new_filter_only()
    {
        var alerts = Seed();
        alerts.MarkAllNewAsSeen();
        Assert.Empty(ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.New));
        Assert.Equal(4, ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All).Count);
        Assert.Equal(2, ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Critical).Count);
    }

    [Fact]
    public void Compact_bar_counter_selects_matching_filter_and_opens_list()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.New);
        Assert.Equal(ProblemListFilter.New, state.ActiveFilter);
        Assert.True(state.IsExpanded);

        state.ToggleCounter(ProblemListFilter.Critical);
        Assert.Equal(ProblemListFilter.Critical, state.ActiveFilter);
        Assert.True(state.IsExpanded);
        state.ToggleCounter(ProblemListFilter.Warning);
        Assert.Equal(ProblemListFilter.Warning, state.ActiveFilter);
        state.ToggleCounter(ProblemListFilter.Unknown);
        Assert.Equal(ProblemListFilter.Unknown, state.ActiveFilter);
        Assert.True(state.IsExpanded);
    }

    [Fact]
    public void Closed_list_plus_new_opens_new()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.New);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.New, state.ActiveFilter);
    }

    [Fact]
    public void New_plus_new_closes()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.New);
        state.ToggleCounter(ProblemListFilter.New);
        Assert.False(state.IsExpanded);
        Assert.Equal(ProblemListFilter.New, state.ActiveFilter);
    }

    [Fact]
    public void Closed_list_plus_crit_opens_crit()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Critical);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Critical, state.ActiveFilter);
    }

    [Fact]
    public void Crit_plus_crit_closes()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Critical);
        state.ToggleCounter(ProblemListFilter.Critical);
        Assert.False(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Critical, state.ActiveFilter);
    }

    [Fact]
    public void Closed_list_plus_warn_opens_warn()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Warning);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Warning, state.ActiveFilter);
    }

    [Fact]
    public void Warn_plus_warn_closes()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Warning);
        state.ToggleCounter(ProblemListFilter.Warning);
        Assert.False(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Warning, state.ActiveFilter);
    }

    [Fact]
    public void Closed_list_plus_unknown_opens_unk()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Unknown);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Unknown, state.ActiveFilter);
    }

    [Fact]
    public void Unk_plus_unknown_closes()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Unknown);
        state.ToggleCounter(ProblemListFilter.Unknown);
        Assert.False(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Unknown, state.ActiveFilter);
    }

    [Fact]
    public void Crit_plus_warn_stays_open_and_switches()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Critical);
        state.ToggleCounter(ProblemListFilter.Warning);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Warning, state.ActiveFilter);
    }

    [Fact]
    public void Warn_plus_new_stays_open_and_switches()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Warning);
        state.ToggleCounter(ProblemListFilter.New);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.New, state.ActiveFilter);
    }

    [Fact]
    public void New_plus_crit_stays_open_and_switches()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.New);
        state.ToggleCounter(ProblemListFilter.Critical);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Critical, state.ActiveFilter);
    }

    [Fact]
    public void Unknown_plus_warn_stays_open_and_switches()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Unknown);
        state.ToggleCounter(ProblemListFilter.Warning);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Warning, state.ActiveFilter);
    }

    [Fact]
    public void Filter_switch_uses_the_same_view_state_without_collapsing()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Critical);
        Assert.True(state.IsExpanded);
        state.ToggleCounter(ProblemListFilter.Warning);
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Warning, state.ActiveFilter);
    }

    [Fact]
    public void Gear_does_not_select_or_change_filter()
    {
        var state = new ProblemListViewState();
        state.OpenFilter(ProblemListFilter.Warning);

        // Gear / Settings / About / mute / hide are shell commands. They must not call
        // OpenFilter or ToggleFromBarBackground.
        Assert.Equal(ProblemListFilter.Warning, state.ActiveFilter);
        Assert.True(state.IsExpanded);
    }

    [Fact]
    public void Collapse_keeps_filter_until_bar_background_reopens_as_all()
    {
        var state = new ProblemListViewState();
        state.OpenFilter(ProblemListFilter.New);
        state.Collapse();
        Assert.Equal(ProblemListFilter.New, state.ActiveFilter);
        Assert.False(state.IsExpanded);

        state.ToggleFromBarBackground();
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.All, state.ActiveFilter);
    }

    [Fact]
    public void Bar_background_opens_all_and_does_not_keep_previous_filter()
    {
        var state = new ProblemListViewState();
        state.OpenFilter(ProblemListFilter.Critical);
        state.ToggleFromBarBackground();
        Assert.False(state.IsExpanded);
        state.ToggleFromBarBackground();
        Assert.True(state.IsExpanded);
        Assert.Equal(ProblemListFilter.All, state.ActiveFilter);
    }

    [Fact]
    public void Taken_returns_only_notifier_taken_incidents()
    {
        var alerts = SeedSearch();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Taken);
        var item = Assert.Single(result);
        Assert.True(item.IsTakenByNotifier);
        Assert.Equal("Michał", item.TakenByDisplayName);
    }

    [Fact]
    public void Taken_excludes_generic_manual_ack()
    {
        var alerts = SeedSearch();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Taken);
        Assert.DoesNotContain(result, incident => incident.ObjectId.Equals(ProblemFactory.ServiceId("mail01", "Queue")));
        Assert.Contains(
            alerts.GetOpenIncidents(),
            incident => incident.ObjectId.Equals(ProblemFactory.ServiceId("mail01", "Queue"))
                        && incident.IsAcknowledgedInCheckmk
                        && !incident.IsTakenByNotifier);
    }

    [Fact]
    public void Taken_count_is_notifier_taken_only()
    {
        var alerts = SeedSearch();
        Assert.Equal(1, ProblemListFilterLogic.CountTaken(alerts.GetOpenIncidents()));
    }

    [Fact]
    public void Search_matches_host_name()
    {
        var alerts = SeedSearch();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All, "KIDL");
        var item = Assert.Single(result);
        Assert.Equal("KIDL", item.ObjectId.HostName);
    }

    [Fact]
    public void Search_matches_service_description()
    {
        var alerts = SeedSearch();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All, "Update");
        var item = Assert.Single(result);
        Assert.Equal("Windows Update", item.ObjectId.ServiceDescription);
    }

    [Fact]
    public void Search_is_case_insensitive()
    {
        var alerts = SeedSearch();
        var lower = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All, "kidl");
        var upper = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All, "KIDL");
        Assert.Equal(lower.Select(i => i.ObjectId), upper.Select(i => i.ObjectId));
        Assert.Single(lower);
    }

    [Fact]
    public void Search_matches_taken_by_display_name()
    {
        var alerts = SeedSearch();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All, "Michał");
        var item = Assert.Single(result);
        Assert.Equal("Michał", item.TakenByDisplayName);
    }

    [Fact]
    public void Search_combines_with_severity_filter()
    {
        var alerts = SeedSearch();
        var result = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Critical, "Update");
        var item = Assert.Single(result);
        Assert.Equal(Severity.Critical, item.Severity);
        Assert.Equal("Windows Update", item.ObjectId.ServiceDescription);
    }

    [Fact]
    public void Search_combines_with_taken_filter()
    {
        var alerts = SeedSearch();
        var byName = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Taken, "Michał");
        Assert.Equal("Michał", Assert.Single(byName).TakenByDisplayName);

        var byHost = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Taken, "KIDL");
        Assert.Empty(byHost);
    }

    [Fact]
    public void Empty_or_whitespace_search_restores_filtered_list()
    {
        var alerts = SeedSearch();
        var all = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All);
        Assert.Equal(all.Count, ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All, "  ").Count);
        Assert.Equal(all.Count, ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All, string.Empty).Count);
        Assert.Equal(all.Count, ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.All, null).Count);
    }

    [Fact]
    public void Search_does_not_mutate_incident_or_seen_state()
    {
        var alerts = SeedSearch();
        var before = alerts.GetOpenIncidents();
        var seenBefore = before.ToDictionary(i => i.ObjectId, i => i.IsSeen);
        _ = ProblemListFilterLogic.Apply(before, ProblemListFilter.Critical, "cpu");
        var after = alerts.GetOpenIncidents();
        Assert.Equal(before.Count, after.Count);
        foreach (var incident in after)
        {
            Assert.Equal(seenBefore[incident.ObjectId], incident.IsSeen);
        }
    }

    [Fact]
    public void Compact_bar_taken_counter_toggles_taken_filter()
    {
        var state = new ProblemListViewState();
        state.ToggleCounter(ProblemListFilter.Taken);
        Assert.Equal(ProblemListFilter.Taken, state.ActiveFilter);
        Assert.True(state.IsExpanded);
        state.ToggleCounter(ProblemListFilter.Taken);
        Assert.False(state.IsExpanded);
        Assert.Equal(ProblemListFilter.Taken, state.ActiveFilter);
    }

    [Fact]
    public void Taken_filter_and_counter_drop_released_incident()
    {
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), _clock);
        var taken = ProblemFactory.Service(
            "web01",
            "CPU",
            Severity.Critical,
            acknowledged: true,
            acknowledgementType: AcknowledgementType.Sticky,
            takenBy: "Michał",
            takenByNotifier: true);
        var other = ProblemFactory.Service(
            "db01",
            "SQL",
            Severity.Warning,
            acknowledged: true,
            acknowledgementType: AcknowledgementType.Sticky,
            takenBy: "Paweł",
            takenByNotifier: true);
        alerts.ApplySnapshot(ProblemFactory.Ok(_clock.UtcNow, taken, other));
        alerts.MarkSeen(taken.Id);
        Assert.Equal(2, ProblemListFilterLogic.CountTaken(alerts.GetOpenIncidents()));

        alerts.ApplySnapshot(ProblemFactory.Ok(
            _clock.UtcNow,
            ProblemFactory.Service("web01", "CPU", Severity.Critical),
            other));

        var remaining = ProblemListFilterLogic.Apply(alerts.GetOpenIncidents(), ProblemListFilter.Taken);
        var item = Assert.Single(remaining);
        Assert.Equal("Paweł", item.TakenByDisplayName);
        Assert.Equal(1, ProblemListFilterLogic.CountTaken(alerts.GetOpenIncidents()));
        Assert.True(Assert.Single(alerts.GetOpenIncidents(), incident => incident.ObjectId.Equals(taken.Id)).IsSeen);
    }

    private AlertStateService Seed()
    {
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), _clock);
        alerts.ApplySnapshot(ProblemFactory.Ok(
            _clock.UtcNow,
            ProblemFactory.Service("web01", "CPU", Severity.Critical),
            ProblemFactory.Service("web01", "Disk", Severity.Warning),
            ProblemFactory.Service("db01", "SQL", Severity.Critical),
            ProblemFactory.Service("edge", "Agent", Severity.Unknown)));
        return alerts;
    }

    private AlertStateService SeedSearch()
    {
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), _clock);
        alerts.ApplySnapshot(ProblemFactory.Ok(
            _clock.UtcNow,
            ProblemFactory.Host("KIDL", Severity.Critical),
            ProblemFactory.Service("web01", "Windows Update", Severity.Critical),
            ProblemFactory.Service(
                "db01",
                "SQL",
                Severity.Warning,
                acknowledged: true,
                acknowledgementType: AcknowledgementType.Sticky,
                takenBy: "Michał",
                takenByNotifier: true),
            ProblemFactory.Service(
                "mail01",
                "Queue",
                Severity.Critical,
                acknowledged: true,
                acknowledgementType: AcknowledgementType.Sticky)));
        return alerts;
    }
}
