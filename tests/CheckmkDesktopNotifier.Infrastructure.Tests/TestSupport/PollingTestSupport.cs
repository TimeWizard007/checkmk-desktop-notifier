using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Polling;

namespace CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

internal sealed class RecordingCheckmkClient : ICheckmkClient
{
    private readonly object _gate = new();
    private readonly List<DateTimeOffset> _callTimes = [];

    public RecordingCheckmkClient(TimeProvider clock)
    {
        Clock = clock;
    }

    public TimeProvider Clock { get; }

    public int Calls
    {
        get
        {
            lock (_gate)
            {
                return _callTimes.Count;
            }
        }
    }

    public IReadOnlyList<DateTimeOffset> CallTimes
    {
        get
        {
            lock (_gate)
            {
                return _callTimes.ToArray();
            }
        }
    }

    public Func<CancellationToken, Task<ProblemSnapshot>>? Handler { get; set; }

    public ProblemSnapshot Snapshot { get; set; } = ProblemSnapshot.Failure(
        DateTimeOffset.UnixEpoch,
        SnapshotErrorKind.Unavailable,
        "No snapshot configured.");

    public TaskCompletionSource? Gate { get; set; }

    public async Task<ProblemSnapshot> GetCurrentProblemsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _callTimes.Add(Clock.GetUtcNow());
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Handler is not null)
        {
            return await Handler(cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Snapshot;
    }
}

internal static class PollerTestHost
{
    public static (CheckmkPoller Poller, AlertStateService Alerts, RecordingCheckmkClient Client) Create(
        CheckmkOptions? options = null,
        TimeProvider? clock = null,
        PollDiagnosticsWriter? diagnostics = null,
        INotificationCoordinator? notifications = null)
    {
        clock ??= TimeProvider.System;
        options ??= new CheckmkOptions { Mode = ClientMode.Mock, PollIntervalSeconds = 60 };
        var client = new RecordingCheckmkClient(clock);
        var alerts = new AlertStateService(new InMemoryAlertStateStore(), clock);
        var poller = new CheckmkPoller(client, alerts, options, clock, diagnostics, notifications);
        return (poller, alerts, client);
    }
}
