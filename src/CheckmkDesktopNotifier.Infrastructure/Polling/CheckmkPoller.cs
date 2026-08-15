using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Polling;

public sealed class CheckmkPoller : IProblemPoller
{
    private readonly ICheckmkClient _client;
    private readonly IAlertStateService _alerts;
    private readonly TimeProvider _clock;
    private readonly PollDiagnosticsWriter? _diagnostics;
    private readonly SemaphoreSlim _flight = new(1, 1);
    private readonly object _statusLock = new();
    private ConnectionStatus _status = ConnectionStatus.Idle;

    public CheckmkPoller(
        ICheckmkClient client,
        IAlertStateService alerts,
        CheckmkOptions options,
        TimeProvider? clock = null,
        PollDiagnosticsWriter? diagnostics = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        ArgumentNullException.ThrowIfNull(options);
        CheckmkOptionsValidator.Validate(options);
        Interval = options.PollInterval;
        _clock = clock ?? TimeProvider.System;
        _diagnostics = diagnostics;
    }

    public TimeSpan Interval { get; }

    public ConnectionStatus Status
    {
        get
        {
            lock (_statusLock)
            {
                return _status;
            }
        }
    }

    public event EventHandler? StateChanged;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!await _flight.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await PollCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _flight.Release();
        }
    }

    public async Task RefreshWhenIdleAsync(CancellationToken cancellationToken = default)
    {
        await _flight.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PollCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _flight.Release();
        }
    }

    public async Task RunLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var started = _clock.GetUtcNow();
                await RefreshAsync(cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var remaining = Interval - (_clock.GetUtcNow() - started);
                if (remaining <= TimeSpan.Zero)
                {
                    continue;
                }

                await DelayAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PollCoreAsync(CancellationToken cancellationToken)
    {
        SetStatus(new ConnectionStatus(ConnectionStatusKind.Refreshing, _alerts.LastSuccessfulPollUtc, null));

        ProblemSnapshot snapshot;
        try
        {
            snapshot = await _client.GetCurrentProblemsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus(new ConnectionStatus(ConnectionStatusKind.Error, _alerts.LastSuccessfulPollUtc, null));
            throw;
        }
        catch (Exception)
        {
            snapshot = ProblemSnapshot.Failure(
                _clock.GetUtcNow(),
                SnapshotErrorKind.Unavailable,
                "The Checkmk request failed.");
        }

        _alerts.ApplySnapshot(snapshot);
        var lastSuccess = _alerts.LastSuccessfulPollUtc;
        var now = _clock.GetUtcNow();

        if (snapshot.IsSuccess)
        {
            _diagnostics?.WriteSuccess(now, snapshot.Problems);
            SetStatus(new ConnectionStatus(ConnectionStatusKind.Connected, lastSuccess, null));
            return;
        }

        _diagnostics?.WriteFailure(now, snapshot.ErrorKind, snapshot.ErrorMessage);
        SetStatus(new ConnectionStatus(
            ConnectionStatusKind.Error,
            lastSuccess,
            snapshot.ErrorMessage ?? "Connection error"));
    }

    private void SetStatus(ConnectionStatus status)
    {
        lock (_statusLock)
        {
            _status = status;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        using var timer = _clock.CreateTimer(
            _ => tcs.TrySetResult(),
            state: null,
            delay,
            Timeout.InfiniteTimeSpan);
        await tcs.Task.ConfigureAwait(false);
    }
}
