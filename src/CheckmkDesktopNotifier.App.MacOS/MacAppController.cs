using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS;

public sealed class MacAppController : IDisposable
{
    private readonly MacProblemListViewModel _problems;
    private readonly MacConnectionViewModel _settings;
    private readonly IMacStatusItem _statusItem;
    private readonly LoadedConfiguration _loaded;
    private readonly IUiThread _uiThread;
    private readonly MacHostErrorLog _errors;
    private readonly MacSingleInstanceToggle<ProblemPanelWindow> _panelLifetime = new();
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MacStatusItemEventRouter? _statusEvents;
    private ProblemPanelWindow? _panel;
    private MainWindow? _settingsWindow;
    private bool _allowExit;

    public MacAppController(
        MacProblemListViewModel problems,
        MacConnectionViewModel settings,
        IMacStatusItem statusItem,
        LoadedConfiguration loaded,
        IUiThread uiThread,
        MacHostErrorLog errors)
    {
        _problems = problems ?? throw new ArgumentNullException(nameof(problems));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _statusItem = statusItem ?? throw new ArgumentNullException(nameof(statusItem));
        _loaded = loaded ?? throw new ArgumentNullException(nameof(loaded));
        _uiThread = uiThread ?? throw new ArgumentNullException(nameof(uiThread));
        _errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public int ProblemPanelCreateCount => _panelLifetime.CreateCount;

    public void Attach(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _panel = _panelLifetime.GetOrCreate(() =>
        {
            var panel = new ProblemPanelWindow
            {
                DataContext = _problems
            };
            panel.Closing += OnPanelClosing;
            return panel;
        });

        _settingsWindow = new MainWindow
        {
            DataContext = _settings,
            Title = "Settings"
        };
        _settingsWindow.Closing += OnSettingsClosing;
        _settings.Saved += (_, _) =>
        {
            _problems.Reload();
            HideSettings();
        };

        _problems.RequestSettings = ShowSettings;
        _problems.RequestOpenSite = () => _settings.OpenCheckmkCommand.Execute(null);
        _problems.RequestQuit = Quit;

        var hidden = new Window
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            ShowInTaskbar = false,
            ShowActivated = false,
            SystemDecorations = SystemDecorations.None
        };
        hidden.Opened += (_, _) => hidden.Hide();
        desktop.MainWindow = hidden;

        _statusEvents = new MacStatusItemEventRouter(
            _statusItem,
            MarshalFromNative,
            new MacStatusItemCommands
            {
                ToggleProblems = ToggleProblems,
                ShowProblems = ShowProblems,
                ShowSettings = ShowSettings,
                OpenCheckmk = () => _settings.OpenCheckmkCommand.Execute(null),
                Quit = Quit
            },
            _errors.Write);
        _problems.MenuBarChanged += (_, _) => ApplyMenuBar();
        ApplyMenuBar();

        _problems.StartListening();
        _settings.StartListening();

        if (MacStartupPolicy.ShowSettingsOnStartup(_loaded.NeedsFirstRunSetup))
        {
            ShowSettings();
        }
    }

    public void ShowProblems()
    {
        RunUi(() =>
        {
            if (_panel is null)
            {
                return;
            }

            TryPositionPanel(_panel);
            _panel.Show();
            _panel.Activate();
        });
    }

    public void HideProblems()
    {
        RunUi(() => _panel?.Hide());
    }

    public void ToggleProblems()
    {
        if (_panel is { IsVisible: true })
        {
            HideProblems();
            return;
        }

        ShowProblems();
    }

    public void ShowSettings()
    {
        RunUi(() =>
        {
            if (_settingsWindow is null)
            {
                return;
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();
        });
    }

    public void HideSettings()
    {
        RunUi(() => _settingsWindow?.Hide());
    }

    public void Quit()
    {
        RunUi(() =>
        {
            _allowExit = true;
            _desktop?.Shutdown();
        });
    }

    public void Dispose()
    {
        _statusEvents?.Dispose();
        _statusEvents = null;
        _statusItem.Dispose();
    }

    private void MarshalFromNative(Action action)
    {
        if (_uiThread is AvaloniaUiThread avalonia)
        {
            avalonia.PostDeferred(action);
            return;
        }

        _uiThread.Post(action);
    }

    private void RunUi(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _errors.Write(ex);
        }
    }

    private void ApplyMenuBar()
    {
        _statusItem.SetTitle(_problems.MenuBarTitle);
        _statusItem.SetToolTip(_problems.MenuBarToolTip);
    }

    private void TryPositionPanel(Window panel)
    {
        try
        {
            PositionPanel(panel);
        }
        catch (Exception ex)
        {
            _errors.Write(ex);
        }
    }

    private void PositionPanel(Window panel)
    {
        if (!_statusItem.TryGetAnchor(out var anchor))
        {
            var screen = panel.Screens.Primary?.WorkingArea
                         ?? new PixelRect(0, 0, 1280, 800);
            panel.Position = new PixelPoint(
                screen.X + screen.Width - (int)panel.Width - 24,
                screen.Y + 28);
            return;
        }

        var desktop = panel.Screens.Primary?.Bounds
                      ?? new PixelRect(0, 0, 1280, 800);
        var topOfItem = desktop.Height - (int)Math.Round(anchor.Y + anchor.Height);
        var x = (int)Math.Round(anchor.X + desktop.X);
        var y = desktop.Y + topOfItem + (int)Math.Round(anchor.Height) + 6;
        panel.Position = new PixelPoint(x, Math.Max(desktop.Y + 24, y));
    }

    private void OnPanelClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowExit)
        {
            return;
        }

        e.Cancel = true;
        HideProblems();
    }

    private void OnSettingsClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowExit)
        {
            return;
        }

        e.Cancel = true;
        HideSettings();
    }
}
