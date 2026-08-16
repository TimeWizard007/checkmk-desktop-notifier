using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using Microsoft.Extensions.Hosting;

namespace CheckmkDesktopNotifier.Infrastructure;

public sealed class CheckmkPollingHostedService : BackgroundService
{
    private readonly CheckmkOptions _options;
    private readonly IProblemPoller _poller;
    private readonly IMonitoringCoordinator? _coordinator;

    public CheckmkPollingHostedService(
        CheckmkOptions options,
        IProblemPoller poller,
        IMonitoringCoordinator? coordinator = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _coordinator = coordinator;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_coordinator is not null)
        {
            return _coordinator.RunPollingAsync(_poller, stoppingToken);
        }

        if (!CheckmkRuntimeProfile.UseBackgroundPolling(_options.Mode))
        {
            return Task.CompletedTask;
        }

        return _poller.RunLoopAsync(stoppingToken);
    }
}
