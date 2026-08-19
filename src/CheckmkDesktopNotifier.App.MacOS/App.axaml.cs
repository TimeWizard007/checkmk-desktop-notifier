using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CheckmkDesktopNotifier.App.MacOS;

public partial class App : Application
{
    private MacDesktopHost? _host;
    private MacAppController? _controller;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = MacDesktopHost.Create();
            _controller = _host.Services.GetRequiredService<MacAppController>();
            desktop.ShutdownRequested += OnShutdownRequested;

            var loaded = _host.Services.GetRequiredService<LoadedConfiguration>();
            if (MacStartupPolicy.StartPollingOnStartup(loaded.IsUsableReal))
            {
                var coordinator = _host.Services.GetRequiredService<IMonitoringCoordinator>();
                try
                {
                    await coordinator.ApplyAsync(loaded.Options).ConfigureAwait(true);
                }
                catch (Exception)
                {
                }
            }

            await _host.StartAsync().ConfigureAwait(true);
            _controller.Attach(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _controller?.Dispose();
        _controller = null;
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(true);
            await _host.DisposeAsync().ConfigureAwait(true);
            _host = null;
        }
    }
}
