using System.IO;
using System.Text.Json;
using System.Windows;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.App.Mock;
using CheckmkDesktopNotifier.App.ViewModels;
using CheckmkDesktopNotifier.App.Views;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Autostart;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CheckmkDesktopNotifier.App;

public partial class App : Application
{
    private IHost? _host;
    private SingleInstanceGuard? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceGuard.TryOwn(out _singleInstance))
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var paths = AppStoragePaths.ForCurrentUser();
        ISecretStore secrets = OperatingSystem.IsWindows()
            ? new WindowsCredentialSecretStore()
            : new InMemorySecretStore();
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
                Options = new CheckmkOptions { Mode = ClientMode.Real, PollIntervalSeconds = CheckmkOptions.DefaultPollIntervalSeconds },
                Source = ConfigurationSource.None,
                IsUsableReal = false,
                IsMock = false,
                LoadError = "Saved settings could not be read. Open Settings to repair the configuration."
            };
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton(paths);
                services.AddSingleton(secrets);
                services.AddSingleton<IUserSettingsStore>(settingsStore);
                services.AddSingleton(gui);
                services.AddSingleton(loaded);
                services.AddSingleton<CheckmkConnectionTester>();
                services.AddSingleton<ILocalizationService, LocalizationService>();
                services.AddSingleton<IUriLauncher, ShellUriLauncher>();
                services.AddSingleton<WindowSessionState>();
                services.AddSingleton<IUserPreferences>(new JsonUserPreferencesStore(paths.PreferencesPath));
                services.AddSingleton<NotificationSoundStore>();
                services.AddSingleton<DeferredNotificationService>();
                services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<DeferredNotificationService>());
                services.AddSingleton<IAlertSoundService, WindowsAlertSoundService>();
                services.AddSingleton<INotificationCoordinator, NotificationCoordinator>();
                services.AddSingleton<IApplicationExecutable, CurrentProcessExecutable>();
                services.AddSingleton<IAutostartStore>(_ => OperatingSystem.IsWindows()
                    ? new WindowsHkcuRunAutostartStore()
                    : new InMemoryAutostartStore());
                services.AddSingleton<AutostartService>();

                if (loaded.IsMock)
                {
                    services.AddSingleton<IAlertStateStore, InMemoryAlertStateStore>();
                    services.AddSingleton<IAlertStateService, AlertStateService>();
                    services.AddCheckmkClient(loaded.Options);
                }
                else
                {
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
                }

                services.AddCheckmkPolling(paths.LastPollPath);
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<CompactBarWindow>();
                services.AddSingleton<ProblemListWindow>();
                services.AddSingleton<UiShell>();
                services.AddSingleton<IShellCommands>(sp => sp.GetRequiredService<UiShell>());
                services.AddSingleton(sp => new Lazy<IShellCommands>(sp.GetRequiredService<IShellCommands>));
            })
            .Build();

        var bar = _host.Services.GetRequiredService<CompactBarWindow>();
        MainWindow = bar;
        var shell = _host.Services.GetRequiredService<UiShell>();
        try
        {
            _host.Services.GetRequiredService<AutostartService>().RepairIfRegistered();
        }
        catch (Exception)
        {
        }
        var viewModel = _host.Services.GetRequiredService<ShellViewModel>();
        shell.Show();
        _singleInstance?.Listen(() => Dispatcher.BeginInvoke(new Action(shell.ShowBar)));

        var client = _host.Services.GetRequiredService<ICheckmkClient>();
        var alerts = _host.Services.GetRequiredService<IAlertStateService>();
        var clock = _host.Services.GetRequiredService<TimeProvider>();

        if (loaded.IsMock)
        {
            await DemoBootstrapper.InitializeAsync(client, alerts, clock).ConfigureAwait(true);
        }
        else if (loaded.IsUsableReal)
        {
            var coordinator = _host.Services.GetRequiredService<IMonitoringCoordinator>();
            try
            {
                await coordinator.ApplyAsync(loaded.Options).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is CheckmkOptionsValidationException or InvalidOperationException or IOException)
            {
                MessageBox.Show(
                    ConnectionTestResult.Sanitize(ex.Message),
                    "Checkmk Desktop Notifier",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        await _host.StartAsync().ConfigureAwait(true);
        viewModel.CompleteInitialization();

        if (loaded.NeedsFirstRunSetup)
        {
            shell.ShowSettings();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(true);
            _host.Dispose();
            _host = null;
        }

        _singleInstance?.Dispose();
        _singleInstance = null;

        base.OnExit(e);
    }
}
