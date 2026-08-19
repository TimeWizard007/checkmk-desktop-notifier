using System.Drawing;
using System.Windows;
using CheckmkDesktopNotifier.App.Localization;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using Forms = System.Windows.Forms;

namespace CheckmkDesktopNotifier.App;

/// <summary>
/// WinForms tray icon and balloon tips. Notification policy is <see cref="INotificationCoordinator"/>;
/// this type is Windows presentation only.
/// </summary>
public sealed class NotifyIconTray : INotificationService, IDisposable
{
    private static readonly Color MenuBackground = Color.FromArgb(255, 37, 42, 51);
    private static readonly Color MenuText = Color.FromArgb(255, 242, 244, 247);
    private static readonly Color MenuMuted = Color.FromArgb(255, 154, 163, 178);

    private readonly IShellCommands _shell;
    private readonly ILocalizationService _text;
    private readonly IUserPreferences _preferences;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _muteItem;
    private readonly Icon _icon;
    private bool _disposed;

    public NotifyIconTray(
        IShellCommands shell,
        ILocalizationService text,
        IUserPreferences preferences,
        Icon icon)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _icon = icon ?? throw new ArgumentNullException(nameof(icon));

        _menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false,
            BackColor = MenuBackground,
            ForeColor = MenuText,
            Font = new Font("Segoe UI", 9f),
            Padding = new Forms.Padding(3, 3, 3, 3),
            Renderer = new DarkTrayMenuRenderer()
        };

        AddItem(_text.MenuOpen, _shell.ShowBar);
        AddItem(_text.MenuConnectionSettings, _shell.ShowSettings);
        AddItem(_text.MenuHelpAbout, _shell.ShowAbout);
        _muteItem = AddItem(MuteHeader(), () => MuteCommands.Toggle(_preferences));
        _menu.Items.Add(CreateSeparator());
        var exit = AddItem(_text.MenuExit, _shell.Exit);
        exit.ForeColor = MenuMuted;

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = _text.CompactBarTitle,
            ContextMenuStrip = _menu
        };
        _notifyIcon.MouseUp += OnMouseUp;
        _preferences.Changed += OnPreferencesChanged;
    }

    public void Show(IncidentAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        Dispatch(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var icon = alert.Severity == Severity.Warning
                    ? Forms.ToolTipIcon.Warning
                    : Forms.ToolTipIcon.Error;
                _notifyIcon.ShowBalloonTip(8000, alert.Title, alert.Body, icon);
            }
            catch (Exception)
            {
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _preferences.Changed -= OnPreferencesChanged;
        _notifyIcon.MouseUp -= OnMouseUp;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private void OnPreferencesChanged(object? sender, EventArgs e) =>
        Dispatch(() =>
        {
            if (!_disposed)
            {
                _muteItem.Text = MuteHeader();
            }
        });

    private string MuteHeader() => MuteCommands.MenuHeader(_preferences, _text.MenuMuteSound, _text.MenuUnmuteSound);

    private Forms.ToolStripMenuItem AddItem(string text, Action command)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            BackColor = MenuBackground,
            ForeColor = MenuText,
            Padding = new Forms.Padding(10, 3, 10, 3)
        };
        item.Click += (_, _) => Dispatch(command);
        _menu.Items.Add(item);
        return item;
    }

    private static Forms.ToolStripSeparator CreateSeparator() =>
        new()
        {
            AutoSize = false,
            Size = new System.Drawing.Size(120, 1),
            Height = 1,
            BackColor = MenuBackground,
            ForeColor = Color.FromArgb(140, 58, 65, 80),
            Margin = new Forms.Padding(8, 2, 8, 2),
            Padding = Forms.Padding.Empty
        };

    private void OnMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            Dispatch(_shell.ToggleBar);
        }
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    public static Icon LoadApplicationIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute))
                       ?? throw new InvalidOperationException("The application icon resource is missing.");
        using var stream = resource.Stream;
        return new Icon(stream);
    }
}
