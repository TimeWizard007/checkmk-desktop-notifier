using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Mock;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Core.Tests;

public sealed class DemoSnapshotFactoryTests
{
    [Fact]
    public void Demo_snapshot_contains_required_ui_mix()
    {
        var snapshot = DemoSnapshotFactory.Create(ProblemFactory.T0);

        Assert.True(snapshot.IsSuccess);
        Assert.Equal(7, snapshot.Problems.Count);
        Assert.Equal(2, snapshot.Problems.Count(p => p.Severity == Severity.Critical && !p.IsAcknowledgedInCheckmk));
        Assert.True(snapshot.Problems.Count(p => p.Severity == Severity.Warning) >= 2);
        Assert.Contains(snapshot.Problems, p => p.Severity == Severity.Unknown);
        Assert.Contains(snapshot.Problems, p => p.IsAcknowledgedInCheckmk);
        Assert.Contains(snapshot.Problems, p => p.ScheduledDowntimeDepth > 0);
        Assert.Contains(snapshot.Problems, p => p.Id.Kind == ObjectKind.Host);
        Assert.Contains(snapshot.Problems, p => p.Id.Kind == ObjectKind.Service);
        Assert.Equal(DemoSnapshotFactory.MemorySeenId, DemoSnapshotFactory.IncidentToMarkSeen);
    }

    [Fact]
    public void Demo_bootstrap_seen_leaves_new_criticals_and_severity_counts()
    {
        var sut = new AlertStateService(new InMemoryAlertStateStore(), new MutableTimeProvider(ProblemFactory.T0));
        sut.ApplySnapshot(DemoSnapshotFactory.Create(ProblemFactory.T0));
        sut.MarkSeen(DemoSnapshotFactory.IncidentToMarkSeen);

        var open = sut.GetOpenIncidents();
        Assert.Equal(7, open.Count);
        Assert.Equal(6, open.Count(i => !i.IsSeen));
        Assert.Equal(3, open.Count(i => i.Severity == Severity.Critical));
        Assert.Equal(3, open.Count(i => i.Severity == Severity.Warning));
        Assert.Equal(1, open.Count(i => i.Severity == Severity.Unknown));
        Assert.Contains(open, i => i.IsSeen && i.ObjectId == DemoSnapshotFactory.MemorySeenId);
        Assert.Contains(open, i => i.IsAcknowledgedInCheckmk);
        Assert.Contains(open, i => i.ScheduledDowntimeDepth > 0);
        Assert.True(open.Count(i => !i.IsSeen && i.Severity == Severity.Critical) >= 2);
    }

    [Fact]
    public void Last_successful_poll_is_exposed_after_apply()
    {
        var sut = new AlertStateService(new InMemoryAlertStateStore(), new MutableTimeProvider(ProblemFactory.T0));
        Assert.Null(sut.LastSuccessfulPollUtc);

        sut.ApplySnapshot(DemoSnapshotFactory.Create(ProblemFactory.T0));

        Assert.Equal(ProblemFactory.T0, sut.LastSuccessfulPollUtc);
    }
}
