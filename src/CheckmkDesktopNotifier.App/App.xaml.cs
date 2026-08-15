using System.Windows;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.App.Mock;
using CheckmkDesktopNotifier.App.ViewModels;
using CheckmkDesktopNotifier.App.Views;
using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Mock;
using CheckmkDesktopNotifier.Core.Persistence;
using CheckmkDesktopNotifier.Core.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CheckmkDesktopNotifier.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(static services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<IAlertStateStore, InMemoryAlertStateStore>();
                services.AddSingleton<IAlertStateService, AlertStateService>();
                services.AddSingleton<ICheckmkClient, MockCheckmkClient>();
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
        await DemoBootstrapper.InitializeAsync(client, alerts, clock).ConfigureAwait(true);

        var shell = _host.Services.GetRequiredService<UiShell>();
        var bar = _host.Services.GetRequiredService<CompactBarWindow>();
        MainWindow = bar;
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
