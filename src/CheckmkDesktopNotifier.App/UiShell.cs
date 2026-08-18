using System.ComponentModel;
using System.Reflection;
using System.Windows;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.App.ViewModels;
using CheckmkDesktopNotifier.App.Views;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Acknowledgements;
using CheckmkDesktopNotifier.Core.Autostart;
using CheckmkDesktopNotifier.Infrastructure;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Rest;

namespace CheckmkDesktopNotifier.App;

public sealed class UiShell : IShellCommands
{
    private readonly CompactBarWindow _bar;
    private readonly ProblemListWindow _list;
    private readonly ShellViewModel _viewModel;
    private readonly WindowSessionState _session;
    private readonly GuiConfigurationService _gui;
    private readonly CheckmkConnectionTester _tester;
    private readonly ILocalizationService _text;
    private readonly IUriLauncher _uris;
    private readonly IMonitoringCoordinator? _coordinator;
    private readonly IUserPreferences _preferences;
    private readonly DeferredNotificationService _notifications;
    private readonly IAlertSoundService _sound;
    private readonly NotificationSoundStore _sounds;
    private readonly AutostartService _autostart;
    private readonly ShellBarVisibility _barVisibility = new();
    private readonly SingleInstanceGate _settingsGate = new();
    private readonly SingleInstanceGate _aboutGate = new();
    private readonly ShutdownGate _shutdown = new();
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private NotifyIconTray? _tray;

    public UiShell(
        CompactBarWindow bar,
        ProblemListWindow list,
        ShellViewModel viewModel,
        WindowSessionState session,
        GuiConfigurationService gui,
        CheckmkConnectionTester tester,
        ILocalizationService text,
        IUriLauncher uris,
        IUserPreferences preferences,
        DeferredNotificationService notifications,
        IAlertSoundService sound,
        NotificationSoundStore sounds,
        AutostartService autostart,
        IMonitoringCoordinator? coordinator = null)
    {
        _bar = bar;
        _list = list;
        _viewModel = viewModel;
        _session = session;
        _gui = gui;
        _tester = tester;
        _text = text;
        _uris = uris;
        _preferences = preferences;
        _notifications = notifications;
        _sound = sound;
        _sounds = sounds;
        _autostart = autostart;
        _coordinator = coordinator;

        _bar.DataContext = _viewModel;
        _list.DataContext = _viewModel;
        _viewModel.ConfirmTake = (title, body) => ShowDarkConfirm(title, body, _text.Take);
        _viewModel.ConfirmRelease = (title, body) => ShowDarkConfirm(title, body, _text.Release);
        _viewModel.ShowTakeMessage = ShowDarkMessage;
        _bar.Icon = AppIcon.WindowSource;
        _list.Icon = AppIcon.WindowSource;

        _bar.LocationChanged += (_, _) =>
        {
            _session.BarLeft = _bar.Left;
            _session.BarTop = _bar.Top;
            PositionList();
        };

        _bar.SizeChanged += (_, _) => PositionList();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _list.Closing += OnListClosing;
        _bar.Closing += OnBarClosing;
    }

    public void Show()
    {
        var firstPlace = _session.BarLeft is null || _session.BarTop is null;
        RestoreOrPlaceBar();
        _barVisibility.Restore();
        _bar.Show();
        if (firstPlace)
        {
            PlaceBarTopRight();
        }
        AttachListOwner();
        PositionList();
        ApplyExpandedState();
        _tray ??= new NotifyIconTray(this, _text, _preferences, NotifyIconTray.LoadApplicationIcon());
        _notifications.SetInner(_tray);
    }

    public void ShowBar()
    {
        if (_shutdown.HasStarted)
        {
            return;
        }

        _barVisibility.Restore();
        ApplyBarVisibility();
    }

    public void HideToTray()
    {
        if (_shutdown.HasStarted || HasOpenDialog)
        {
            return;
        }

        _barVisibility.HideToTray();
        ApplyBarVisibility();
    }

    public void ToggleBar()
    {
        if (_shutdown.HasStarted)
        {
            return;
        }

        if (_barVisibility.IsVisible && HasOpenDialog)
        {
            return;
        }

        _barVisibility.ToggleFromTrayClick();
        ApplyBarVisibility();
    }

    public void ShowSettings()
    {
        if (_shutdown.HasStarted || !_viewModel.IsReady || !_viewModel.SettingsAvailable)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        if (!_settingsGate.TryEnter())
        {
            _settingsWindow?.Activate();
            return;
        }

        var viewModel = new SettingsViewModel(_gui, _tester, _text, _coordinator, _sound, _preferences, _sounds, _autostart);
        var window = new SettingsWindow(viewModel)
        {
            Owner = _bar.IsVisible ? _bar : null,
            Icon = AppIcon.WindowSource
        };
        _settingsWindow = window;
        window.Closed += (_, _) =>
        {
            _settingsWindow = null;
            _settingsGate.Exit();
            _viewModel.Reload();
        };

        window.ShowDialog();
    }

    public void ShowAbout()
    {
        if (_shutdown.HasStarted)
        {
            return;
        }

        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        if (!_aboutGate.TryEnter())
        {
            _aboutWindow?.Activate();
            return;
        }

        var version = ApplicationVersion.FromAssembly(Assembly.GetEntryAssembly() ?? typeof(App).Assembly);
        var window = new AboutWindow
        {
            DataContext = new AboutViewModel(_text, version, _uris),
            Owner = _bar.IsVisible ? _bar : null
        };
        _aboutWindow = window;
        window.Closed += (_, _) =>
        {
            _aboutWindow = null;
            _aboutGate.Exit();
        };
        window.Show();
    }

    public void Exit()
    {
        if (!_shutdown.TryBegin())
        {
            return;
        }

        _viewModel.BeginShutdown();
        _ = ExitCoreAsync();
    }

    private async Task ExitCoreAsync()
    {
        try
        {
            if (_coordinator is not null)
            {
                await _coordinator.ResetPollingAsync().ConfigureAwait(true);
            }
        }
        catch (Exception)
        {
        }

        CloseQuietly(_settingsWindow);
        CloseQuietly(_aboutWindow);
        if (_list.IsVisible)
        {
            _viewModel.IsExpanded = false;
        }

        _notifications.SetInner(null);
        _tray?.Dispose();
        _tray = null;
        Application.Current?.Shutdown();
    }

    private bool HasOpenDialog => _settingsWindow is not null || _aboutWindow is not null;

    private void ApplyBarVisibility()
    {
        if (_barVisibility.IsVisible)
        {
            var firstPlace = _session.BarLeft is null || _session.BarTop is null;
            RestoreOrPlaceBar();
            if (!_bar.IsVisible)
            {
                _bar.Show();
            }

            if (firstPlace)
            {
                PlaceBarTopRight();
            }

            AttachListOwner();
            _bar.Activate();
            _bar.Topmost = false;
            _bar.Topmost = true;
            ApplyExpandedState();
            return;
        }

        if (_list.IsVisible)
        {
            _list.Hide();
        }

        if (_bar.IsVisible)
        {
            _bar.Hide();
        }
    }

    private void OnBarClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdown.HasStarted)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void AttachListOwner()
    {
        if (!_bar.IsLoaded && !_bar.IsVisible)
        {
            return;
        }

        if (!ReferenceEquals(_list.Owner, _bar))
        {
            _list.Owner = _bar;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.IsExpanded) or null)
        {
            ApplyExpandedState();
        }
    }

    private void ApplyExpandedState()
    {
        if (_viewModel.IsExpanded)
        {
            AttachListOwner();
            PositionList();
            if (!_list.IsVisible)
            {
                _list.Show();
            }

            _list.Activate();
            return;
        }

        if (_list.IsVisible)
        {
            _list.Hide();
        }
    }

    private void OnListClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        _viewModel.IsExpanded = false;
    }

    private void RestoreOrPlaceBar()
    {
        if (_session.BarLeft is { } left && _session.BarTop is { } top)
        {
            _bar.Left = left;
            _bar.Top = top;
            return;
        }

        _bar.WindowStartupLocation = WindowStartupLocation.Manual;
        PlaceBarTopRight();
    }

    private void PlaceBarTopRight()
    {
        var work = SystemParameters.WorkArea;
        var width = _bar.ActualWidth > 1 ? _bar.ActualWidth : _bar.DesiredSize.Width;
        if (width < 1)
        {
            _bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width = _bar.DesiredSize.Width;
        }

        const double margin = 12;
        _bar.Left = Math.Max(work.Left, work.Right - Math.Max(width, 1) - margin);
        _bar.Top = work.Top + 12;
        _session.BarLeft = _bar.Left;
        _session.BarTop = _bar.Top;
    }

    private bool ShowDarkConfirm(string title, string body, string confirm)
    {
        Window? owner = _list.IsVisible ? _list : _bar.IsVisible ? _bar : null;
        var window = new TakeConfirmWindow(title, body, confirm, _text.SettingsCancel)
        {
            Owner = owner,
            Icon = AppIcon.WindowSource,
            Topmost = true
        };
        return TakeConfirmation.ShouldProceed(window.ShowDialog());
    }

    private void ShowDarkMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Window? owner = _list.IsVisible ? _list : _bar.IsVisible ? _bar : null;
        var window = new TakeConfirmWindow(ProductInfo.ProductName, message, _text.AboutClose, cancel: string.Empty)
        {
            Owner = owner,
            Icon = AppIcon.WindowSource,
            Topmost = true
        };
        _ = window.ShowDialog();
    }

    private void PositionList()
    {
        const double gap = 6;
        var work = SystemParameters.WorkArea;
        var listHeight = _list.ActualHeight > 0 ? _list.ActualHeight : _list.Height;
        var below = _bar.Top + _bar.ActualHeight + gap;
        var above = _bar.Top - listHeight - gap;
        var top = below + listHeight <= work.Bottom ? below : Math.Max(work.Top, above);

        _list.Left = Math.Min(_bar.Left, work.Right - _list.Width);
        _list.Top = top;
    }

    private static void CloseQuietly(Window? window)
    {
        try
        {
            window?.Close();
        }
        catch (Exception)
        {
        }
    }
}
