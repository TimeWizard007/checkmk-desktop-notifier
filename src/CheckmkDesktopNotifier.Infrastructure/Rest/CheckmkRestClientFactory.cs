using CheckmkDesktopNotifier.Infrastructure.Configuration;

namespace CheckmkDesktopNotifier.Infrastructure.Rest;

public static class CheckmkRestClientFactory
{
    public static CheckmkRestClient Create(
        CheckmkOptions options,
        TimeProvider clock,
        HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var http = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: false);
        http.BaseAddress = options.CreateApiBaseUri();
        http.Timeout = options.CreateHttpTimeout();
        http.DefaultRequestHeaders.ExpectContinue = false;
        return new CheckmkRestClient(http, options, clock);
    }
}
