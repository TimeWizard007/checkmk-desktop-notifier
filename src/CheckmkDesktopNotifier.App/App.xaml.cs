using System.IO;
using System.Text.Json;
using System.Windows;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.App.Mock;
using CheckmkDesktopNotifier.App.ViewModels;
using CheckmkDesktopNotifier.App.Views;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CheckmkDesktopNotifier.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CheckmkOptions options;
        try
        {
            options = CheckmkOptionsLoader.Load();
            CheckmkOptionsValidator.Validate(options);
        }
        catch (Exception ex) when (ex is CheckmkOptionsValidationException or JsonException or System.IO.IOException)
        {
            MessageBox.Show(
                ex.Message,
                "Checkmk Desktop Notifier",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CheckmkDesktopNotifier");
        Directory.CreateDirectory(appData);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(TimeProvider.System);
                if (CheckmkRuntimeProfile.UsePersistentAlertState(options.Mode))
                {
                    var statePath = Path.Combine(appData, "alert-state.json");
                    services.AddSingleton<IAlertStateStore>(_ => new JsonAlertStateStore(statePath));
                }
                else
                {
                    services.AddSingleton<IAlertStateStore, InMemoryAlertStateStore>();
                }

                services.AddSingleton<IAlertStateService, AlertStateService>();
                services.AddCheckmkClient(options);
                services.AddCheckmkPolling(Path.Combine(appData, "last-poll.txt"));
                services.AddSingleton<ILocalizationService, LocalizationService>();
                services.AddSingleton<WindowSessionState>();
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<CompactBarWindow>();
                services.AddSingleton<ProblemListWindow>();
                services.AddSingleton<UiShell>();
            })
            .Build();

        var client = _host.Services.GetRequiredService<ICheckmkClient>();
        var alerts = _host.Services.GetRequiredService<IAlertStateService>();
        var clock = _host.Services.GetRequiredService<TimeProvider>();

        if (CheckmkRuntimeProfile.UseDemoBootstrap(options.Mode))
        {
            await DemoBootstrapper.InitializeAsync(client, alerts, clock).ConfigureAwait(true);
        }

        var bar = _host.Services.GetRequiredService<CompactBarWindow>();
        MainWindow = bar;
        var shell = _host.Services.GetRequiredService<UiShell>();

        await _host.StartAsync().ConfigureAwait(true);
        shell.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(true);
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
