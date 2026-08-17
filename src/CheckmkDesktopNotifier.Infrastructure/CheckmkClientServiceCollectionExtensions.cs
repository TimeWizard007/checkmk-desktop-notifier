using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Mock;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Polling;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using Microsoft.Extensions.DependencyInjection;

namespace CheckmkDesktopNotifier.Infrastructure;

public static class CheckmkClientServiceCollectionExtensions
{
    public static IServiceCollection AddCheckmkClient(this IServiceCollection services, CheckmkOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        CheckmkOptionsValidator.Validate(options);

        services.AddSingleton(options);

        if (options.Mode == ClientMode.Mock)
        {
            services.AddSingleton<ICheckmkClient, MockCheckmkClient>();
            return services;
        }

        services.AddHttpClient<ICheckmkClient, CheckmkRestClient>((_, client) =>
        {
            client.BaseAddress = options.CreateApiBaseUri();
            client.Timeout = options.CreateHttpTimeout();
            client.DefaultRequestHeaders.ExpectContinue = false;
        });

        return services;
    }

    public static IServiceCollection AddCheckmkPolling(
        this IServiceCollection services,
        string? diagnosticsFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!string.IsNullOrWhiteSpace(diagnosticsFilePath))
        {
            services.AddSingleton(new PollDiagnosticsWriter(diagnosticsFilePath));
        }

        services.AddSingleton<IProblemPoller>(sp =>
            new CheckmkPoller(
                sp.GetRequiredService<ICheckmkClient>(),
                sp.GetRequiredService<IAlertStateService>(),
                sp.GetRequiredService<CheckmkOptions>(),
                sp.GetService<TimeProvider>(),
                sp.GetService<PollDiagnosticsWriter>(),
                sp.GetService<INotificationCoordinator>()));
        services.AddHostedService<CheckmkPollingHostedService>();
        return services;
    }
}
