using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Mock;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
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

        services.AddHttpClient<ICheckmkClient, CheckmkServiceClient>((_, client) =>
        {
            client.BaseAddress = options.CreateApiBaseUri();
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.ExpectContinue = false;
        });

        return services;
    }
}
