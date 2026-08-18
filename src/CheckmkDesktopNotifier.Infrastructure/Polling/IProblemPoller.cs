namespace CheckmkDesktopNotifier.Infrastructure.Polling;

public interface IProblemPoller
{
    ConnectionStatus Status { get; }

    TimeSpan Interval { get; }

    event EventHandler? StateChanged;

    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task RefreshWhenIdleAsync(CancellationToken cancellationToken = default);

    Task RunLoopAsync(CancellationToken cancellationToken = default);

    void SetInterval(TimeSpan interval);
}
