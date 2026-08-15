using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Mock;

namespace CheckmkDesktopNotifier.App.Mock;

public static class DemoBootstrapper
{
    public static async Task InitializeAsync(
        ICheckmkClient client,
        IAlertStateService alerts,
        TimeProvider clock)
    {
        if (client is not MockCheckmkClient mock)
        {
            throw new InvalidOperationException("DemoBootstrapper requires MockCheckmkClient.");
        }

        mock.NextSnapshot = DemoSnapshotFactory.Create(clock.GetUtcNow());
        var snapshot = await mock.GetCurrentProblemsAsync().ConfigureAwait(true);
        alerts.ApplySnapshot(snapshot);
        alerts.MarkSeen(DemoSnapshotFactory.IncidentToMarkSeen);
    }
}
