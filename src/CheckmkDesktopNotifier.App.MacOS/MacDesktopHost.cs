using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Navigation;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Core.Storage;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Secrets;
using CheckmkDesktopNotifier.Platform.MacOS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// macOS composition root. Registers Platform.MacOS implementations only.
/// Does not register Windows Credential Manager, HKCU, WPF, or InMemorySecretStore.
/// </summary>
public sealed class MacDesktopHost : IAsyncDisposable
{
    private MacDesktopHost(IHost host)
    {
        Host = host;
    }

    public IHost Host { get; }

    public IServiceProvider Services => Host.Services;

    public static MacDesktopHost Create()
    {
        var userData = new MacUserDataDirectory();
        var paths = AppStoragePaths.For(userData);
        ISecretStore secrets = new MacKeychainSecretStore();
        var settingsStore = new JsonUserSettingsStore(paths.SettingsPath);
        var gui = new GuiConfigurationService(settingsStore, secrets);

        LoadedConfiguration loaded;
        try
        {
            loaded = CheckmkConfigurationResolver.Resolve(paths, settingsStore, secrets);
        }
        catch (Exception)
        {
            loaded = new LoadedConfiguration
            {
                Options = new CheckmkOptions
                {
                    Mode = ClientMode.Real,
                    PollIntervalSeconds = CheckmkOptions.DefaultPollIntervalSeconds
                },
                Source = ConfigurationSource.None,
                IsUsableReal = false,
                IsMock = false,
                LoadError = "Saved settings could not be read. Repair the connection settings."
            };
        }

        var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<IUserDataDirectory>(userData);
                services.AddSingleton(paths);
                services.AddSingleton<ISecretStore>(secrets);
                services.AddSingleton<IUserSettingsStore>(settingsStore);
                services.AddSingleton(gui);
                services.AddSingleton(loaded);
                services.AddSingleton<CheckmkConnectionTester>();
                services.AddSingleton<IUriLauncher, MacOpenUriLauncher>();
                services.AddSingleton<IUiThread, AvaloniaUiThread>();
                services.AddSingleton<MacHostErrorLog>();
                services.AddSingleton<IMacStatusItem>(sp =>
                {
                    MacNativeCallbackGuard.ErrorSink = sp.GetRequiredService<MacHostErrorLog>().Write;
                    return MacStatusItemFactory.Create();
                });

                IAlertStateStore alertStore = loaded.Identity is not null
                    ? new JsonAlertStateStore(paths.AlertStatePathFor(loaded.Identity), paths.LegacyAlertStatePath)
                    : new InMemoryAlertStateStore();
                services.AddSingleton(alertStore);
                services.AddSingleton<IAlertStateService, AlertStateService>();

                var pollerOptions = loaded.IsUsableReal
                    ? loaded.Options
                    : new CheckmkOptions
                    {
                        Mode = ClientMode.Mock,
                        PollIntervalSeconds = CheckmkOptions.DefaultPollIntervalSeconds
                    };
                services.AddSingleton(pollerOptions);
                services.AddSingleton(new DelegatingCheckmkClient(new UnconfiguredCheckmkClient()));
                services.AddSingleton<ICheckmkClient>(sp => sp.GetRequiredService<DelegatingCheckmkClient>());
                services.AddSingleton<IMonitoringCoordinator, MonitoringCoordinator>();
                services.AddCheckmkPolling(paths.LastPollPath);
                services.AddSingleton<ICheckmkProblemNavigator>(sp =>
                {
                    var current = sp.GetRequiredService<LoadedConfiguration>();
                    var coordinator = sp.GetService<IMonitoringCoordinator>();
                    var launcher = sp.GetRequiredService<IUriLauncher>();
                    return new CheckmkProblemNavigator(
                        () =>
                        {
                            var options = coordinator?.CurrentOptions ?? current.Options;
                            return (options.BaseUrl, options.Site);
                        },
                        launcher.Open);
                });
                services.AddSingleton<MacConnectionViewModel>();
                services.AddSingleton<MacProblemListViewModel>();
                services.AddSingleton<MacAppController>();
            })
            .Build();

        return new MacDesktopHost(host);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        Host.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        Host.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync().ConfigureAwait(false);
        Host.Dispose();
    }
}
