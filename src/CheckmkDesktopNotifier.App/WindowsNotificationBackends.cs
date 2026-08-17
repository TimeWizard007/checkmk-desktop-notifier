using System.IO;
using System.Media;
using System.Windows;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Infrastructure.Configuration;
using CheckmkDesktopNotifier.Infrastructure.Notifications;

namespace CheckmkDesktopNotifier.App;

/// <summary>
/// Holds the WinForms balloon implementation until the tray icon exists.
/// Polling may start only after <see cref="UiShell.Show"/> attaches the inner service.
/// </summary>
public sealed class DeferredNotificationService : INotificationService
{
    private readonly object _gate = new();
    private INotificationService? _inner;

    public void SetInner(INotificationService? inner)
    {
        lock (_gate)
        {
            _inner = inner;
        }
    }

    public void Show(IncidentAlert alert)
    {
        INotificationService? inner;
        lock (_gate)
        {
            inner = _inner;
        }

        inner?.Show(alert);
    }
}

public sealed class WindowsAlertSoundService : IAlertSoundService, IDisposable
{
    private readonly IUserPreferences _preferences;
    private readonly NotificationSoundStore _sounds;
    private readonly byte[] _bundled;
    private readonly object _gate = new();
    private SoundPlayer? _player;
    private MemoryStream? _buffer;
    private bool _disposed;

    public WindowsAlertSoundService(IUserPreferences preferences, NotificationSoundStore sounds)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
        _bundled = LoadBundled();
    }

    public void Play()
    {
        try
        {
            var wav = NotificationSoundMixer.Mix(
                _bundled,
                _sounds.TryReadCustomBytes() ?? [],
                _preferences.SoundSource,
                _preferences.VolumePercent);
            if (wav.Length == 0)
            {
                return;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _player?.Dispose();
                _buffer?.Dispose();
                _buffer = new MemoryStream(wav);
                _player = new SoundPlayer(_buffer);
                _player.Load();
                _player.Play();
            }
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _player?.Dispose();
            _buffer?.Dispose();
        }
    }

    private static byte[] LoadBundled()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri(AlertSoundAsset.PackUri, UriKind.Absolute));
            if (resource?.Stream is null)
            {
                return [];
            }

            using var stream = resource.Stream;
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
