using Avalonia.Platform;
using CheckmkDesktopNotifier.Core;
using CheckmkDesktopNotifier.Core.Notifications;
using CheckmkDesktopNotifier.Core.Threading;
using CheckmkDesktopNotifier.Infrastructure.Notifications;
using CheckmkDesktopNotifier.Platform.MacOS;

namespace CheckmkDesktopNotifier.App.MacOS;

/// <summary>
/// macOS playback for the shared mixer. Does not load Windows SoundPlayer.
/// </summary>
public sealed class MacAlertSoundService : IAlertSoundService
{
    private readonly IUserPreferences _preferences;
    private readonly NotificationSoundStore _sounds;
    private readonly IUiThread? _uiThread;
    private readonly byte[] _bundled;
    private readonly object _gate = new();

    public MacAlertSoundService(
        IUserPreferences preferences,
        NotificationSoundStore sounds,
        IUiThread? uiThread = null)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
        _uiThread = uiThread;
        _bundled = LoadBundled();
    }

    public void Play()
    {
        if (_uiThread is null || _uiThread.CheckAccess())
        {
            PlayCore();
            return;
        }

        _uiThread.Post(PlayCore);
    }

    private void PlayCore()
    {
        try
        {
            byte[] wav;
            lock (_gate)
            {
                wav = NotificationSoundMixer.Mix(
                    _bundled,
                    _sounds.TryReadCustomBytes() ?? [],
                    _preferences.SoundSource,
                    _preferences.VolumePercent);
            }

            if (wav.Length == 0)
            {
                return;
            }

            MacAlertSoundPlayer.TryPlay(wav);
        }
        catch (Exception)
        {
        }
    }

    private static byte[] LoadBundled()
    {
        try
        {
            var uri = new Uri("avares://CheckmkDesktopNotifier.MacOS/Assets/notifier.wav");
            using var stream = AssetLoader.Open(uri);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        catch (Exception)
        {
            try
            {
                var beside = Path.Combine(AppContext.BaseDirectory, "Assets", AlertSoundAsset.FileName);
                return File.Exists(beside) ? File.ReadAllBytes(beside) : [];
            }
            catch (Exception)
            {
                return [];
            }
        }
    }
}
