using CheckmkDesktopNotifier.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;

namespace CheckmkDesktopNotifier.Infrastructure.Polling;

public sealed class CheckmkPollingHostedService : BackgroundService
{
    private readonly CheckmkOptions _options;
    private readonly IProblemPoller _poller;

    public CheckmkPollingHostedService(CheckmkOptions options, IProblemPoller poller)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!CheckmkRuntimeProfile.UseBackgroundPolling(_options.Mode))
        {
            return Task.CompletedTask;
        }

        return _poller.RunLoopAsync(stoppingToken);
    }
}
