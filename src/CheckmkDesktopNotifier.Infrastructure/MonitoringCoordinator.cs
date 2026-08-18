using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Rest;

namespace CheckmkDesktopNotifier.Infrastructure;

public interface IMonitoringCoordinator
{
    CheckmkOptions? CurrentOptions { get; }

    ConnectionIdentity? ActiveIdentity { get; }

    bool IsPollingEnabled { get; }

    Task ApplyAsync(CheckmkOptions options, CancellationToken cancellationToken = default);

    Task ResetPollingAsync();

    Task RunPollingAsync(IProblemPoller poller, CancellationToken stoppingToken);
}

public sealed class MonitoringCoordinator : IMonitoringCoordinator
{
    private readonly DelegatingCheckmkClient _client;
    private readonly DelegatingCheckmkAcknowledgementClient? _acknowledgements;
    private readonly TakeSessionState? _takeSession;
    private readonly IAlertStateService _alerts;
    private readonly IProblemPoller _poller;
    private readonly AppStoragePaths _paths;
    private readonly TimeProvider _clock;
    private readonly HttpMessageHandler? _handler;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private HttpClient? _http;
    private CancellationTokenSource _sessionCts = new();
    private bool _pollingEnabled;
    private CheckmkOptions? _currentOptions;
    private ConnectionIdentity? _activeIdentity;

    public MonitoringCoordinator(
        DelegatingCheckmkClient client,
        IAlertStateService alerts,
        IProblemPoller poller,
        AppStoragePaths paths,
        TimeProvider clock,
        HttpMessageHandler? httpHandler = null,
        DelegatingCheckmkAcknowledgementClient? acknowledgements = null,
        TakeSessionState? takeSession = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _poller = poller ?? throw new ArgumentNullException(nameof(poller));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _handler = httpHandler;
        _acknowledgements = acknowledgements;
        _takeSession = takeSession;
        _sessionCts.Cancel();
    }

    public CheckmkOptions? CurrentOptions
    {
        get
        {
            lock (_gate)
            {
                return _currentOptions;
            }
        }
    }

    public ConnectionIdentity? ActiveIdentity
    {
        get
        {
            lock (_gate)
            {
                return _activeIdentity;
            }
        }
    }

    public bool IsPollingEnabled
    {
        get
        {
            lock (_gate)
            {
                return _pollingEnabled;
            }
        }
    }

    public Task ApplyAsync(CheckmkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        CheckmkOptionsValidator.Validate(options);
        if (options.Mode != ClientMode.Real)
        {
            throw new InvalidOperationException("Monitoring can only be applied in Real mode.");
        }

        var identity = ConnectionIdentity.From(options.BaseUrl!, options.Site!);
        var http = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        http.BaseAddress = options.CreateApiBaseUri();
        http.Timeout = options.CreateHttpTimeout();
        http.DefaultRequestHeaders.ExpectContinue = false;
        var rest = new CheckmkRestClient(http, options, _clock);
        var store = new JsonAlertStateStore(_paths.AlertStatePathFor(identity), _paths.LegacyAlertStatePath);

        lock (_gate)
        {
            CancelSessionUnlocked();
            _http?.Dispose();
            _http = http;
            _client.SetInner(rest);
            _acknowledgements?.SetInner(new CheckmkAcknowledgementClient(http, options));
            _takeSession?.Reset();
            _poller.SetInterval(options.PollInterval);
            if (_activeIdentity is null || !identity.EqualsIdentity(_activeIdentity))
            {
                _alerts.ReplaceStore(store);
            }

            _activeIdentity = identity;
            _currentOptions = options;
            _pollingEnabled = true;
            _sessionCts = new CancellationTokenSource();
        }

        TryWake();
        return Task.CompletedTask;
    }

    public Task ResetPollingAsync()
    {
        lock (_gate)
        {
            CancelSessionUnlocked();
            _http?.Dispose();
            _http = null;
            _client.SetInner(new UnconfiguredCheckmkClient());
            _acknowledgements?.SetInner(new UnavailableCheckmkAcknowledgementClient());
            _takeSession?.Reset();
            _currentOptions = null;
            _pollingEnabled = false;
            _sessionCts = new CancellationTokenSource();
            _sessionCts.Cancel();
        }

        TryWake();
        return Task.CompletedTask;
    }

    public async Task RunPollingAsync(IProblemPoller poller, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(poller);

        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitUntilEnabledAsync(stoppingToken).ConfigureAwait(false);
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            CancellationToken sessionToken;
            lock (_gate)
            {
                if (!_pollingEnabled)
                {
                    continue;
                }

                sessionToken = _sessionCts.Token;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, sessionToken);
            try
            {
                await poller.RunLoopAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
            }
        }
    }

    private async Task WaitUntilEnabledAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            lock (_gate)
            {
                if (_pollingEnabled)
                {
                    return;
                }
            }

            try
            {
                await _wake.WaitAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private void CancelSessionUnlocked()
    {
        try
        {
            _sessionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void TryWake()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }
}
