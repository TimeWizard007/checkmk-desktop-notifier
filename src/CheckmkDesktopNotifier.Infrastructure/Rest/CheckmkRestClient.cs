using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public sealed class CheckmkRestClient : ICheckmkClient
{
    private readonly CheckmkServiceClient _services;
    private readonly CheckmkHostClient _hosts;
    private readonly TimeProvider _clock;
    private readonly CheckmkOptions _options;

    public CheckmkRestClient(HttpClient http, CheckmkOptions options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? TimeProvider.System;
        _services = new CheckmkServiceClient(http, options, _clock);
        _hosts = new CheckmkHostClient(http, options, _clock);
    }

    public async Task<ProblemSnapshot> GetCurrentProblemsAsync(CancellationToken cancellationToken = default)
    {
        var retrievedAt = _clock.GetUtcNow();
        var siteId = new SiteId(_options.Site!);

        var services = await _services.GetCurrentProblemsAsync(cancellationToken).ConfigureAwait(false);
        if (!services.IsSuccess)
        {
            return services;
        }

        var hosts = await _hosts.GetHardHostProblemsAsync(cancellationToken).ConfigureAwait(false);
        if (!hosts.IsSuccess)
        {
            return hosts;
        }

        var merged = new List<MonitoredProblem>(services.Problems.Count + hosts.Problems.Count);
        merged.AddRange(services.Problems);
        merged.AddRange(hosts.Problems);
        return ProblemSnapshot.Success(retrievedAt, siteId, merged);
    }
}
