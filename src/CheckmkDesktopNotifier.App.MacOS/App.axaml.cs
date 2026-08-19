using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using Microsoft.Extensions.DependencyInjection;

namespace CheckmkDesktopNotifier.App.MacOS;

public partial class App : Application
{
    private MacDesktopHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = MacDesktopHost.Create();
            var viewModel = _host.Services.GetRequiredService<MacConnectionViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            desktop.ShutdownRequested += OnShutdownRequested;

            var loaded = _host.Services.GetRequiredService<LoadedConfiguration>();
            if (loaded.IsUsableReal)
            {
                var coordinator = _host.Services.GetRequiredService<IMonitoringCoordinator>();
                try
                {
                    await coordinator.ApplyAsync(loaded.Options).ConfigureAwait(true);
                }
                catch (Exception ex) when (ex is CheckmkOptionsValidationException or InvalidOperationException or IOException)
                {
                    viewModel.SetStatus(ConnectionTestResult.Sanitize(ex.Message));
                }
            }

            await _host.StartAsync().ConfigureAwait(true);
            viewModel.StartListening();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(true);
            await _host.DisposeAsync().ConfigureAwait(true);
            _host = null;
        }
    }
}
